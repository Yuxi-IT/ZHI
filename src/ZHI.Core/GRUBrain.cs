using TorchSharp;
using TorchSharp.Modules;
using ZHI.Shared;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace ZHI.Core;

/// <summary>
/// GRU-based Actor-Critic with dual-head policy (v5: Artificial Life refactor).
/// Action head: 8 actions (move×4, eat, attack, emit_chemical, drink).
/// Chemical head: continuous 0–1 emission strength.
/// Architecture: CNN(7x7x6→288) + NonGrid(46) → GRU(334→128) → FC(128→64) → Actor(8) + Chemical(1) / Critic(1)
/// </summary>
public class GRUBrain : Module
{
    private const int GridChannels = 6;
    private const int GridSize = 7;
    private const int GridFlat = GridSize * GridSize * GridChannels; // 294
    private const int CnnFeatDim = 3 * 3 * 32; // 288

    private readonly Conv2d _conv1;
    private readonly Conv2d _conv2;

    private readonly GRU _gru;
    private readonly Linear _fc;
    private readonly Linear _actorHead;
    private readonly Linear _chemicalHead;
    private readonly Linear _criticHead;

    private optim.Optimizer? _optimizer;
    private readonly float _gamma;
    private readonly float _learningRate;
    private readonly int _hiddenSize;
    private readonly int _gruInputSize;

    public int HiddenSize => _hiddenSize;
    public int GruInputSize => _gruInputSize;

    public GRUBrain() : base("GRUBrain")
    {
        _hiddenSize = 128;
        _gruInputSize = CnnFeatDim + (ToolDefinitions.StateSize - GridFlat); // 288 + 46 = 334

        _conv1 = Conv2d(GridChannels, 16, 3, padding: (long)0);
        _conv2 = Conv2d(16, 32, 3, padding: (long)0);
        _gru = GRU(_gruInputSize, _hiddenSize, 1, batchFirst: false);
        _fc = Linear(_hiddenSize, 64);
        _actorHead = Linear(64, ToolDefinitions.ActionCount);
        _chemicalHead = Linear(64, 1);
        _criticHead = Linear(64, 1);
        _gamma = 0.99f;
        _learningRate = 0.0003f;

        RegisterComponents();
        this.to(Device.TorchDevice);
    }

    public GRUBrain(float learningRate, float gamma) : base("GRUBrain")
    {
        _hiddenSize = 128;
        _gruInputSize = CnnFeatDim + (ToolDefinitions.StateSize - GridFlat);

        _conv1 = Conv2d(GridChannels, 16, 3, padding: (long)0);
        _conv2 = Conv2d(16, 32, 3, padding: (long)0);
        _gru = GRU(_gruInputSize, _hiddenSize, 1, batchFirst: false);
        _fc = Linear(_hiddenSize, 64);
        _actorHead = Linear(64, ToolDefinitions.ActionCount);
        _chemicalHead = Linear(64, 1);
        _criticHead = Linear(64, 1);
        _gamma = gamma;
        _learningRate = learningRate;

        RegisterComponents();
        this.to(Device.TorchDevice);
    }

    private Tensor ProcessState(Tensor state)
    {
        int N = (int)state.shape[0];
        using var grid = state.narrow(1, 0, GridFlat);
        using var nonGrid = state.narrow(1, GridFlat, ToolDefinitions.StateSize - GridFlat);

        using var grid4d = grid.reshape([N, GridChannels, GridSize, GridSize]);

        using var c1 = functional.relu(_conv1.forward(grid4d));
        using var c2 = functional.relu(_conv2.forward(c1));
        using var flat = c2.reshape([N, CnnFeatDim]);

        return cat([flat, nonGrid], dim: 1);
    }

    private Tensor BuildActionMask(Tensor states)
    {
        int N = (int)states.shape[0];
        return torch.ones([N, ToolDefinitions.ActionCount], device: states.device);
    }

    /// <summary>
    /// Single-step forward. Chemical head emits continuous [0,1] value sampled from sigmoid-gaussian.
    /// Returns (actions[N], chemicalValues[N], logProbs[N], values[N], entropy[N], newHidden).
    /// </summary>
    public (Tensor actions, Tensor chemicalValues, Tensor logProbs, Tensor values, Tensor entropy, Tensor newHidden)
        StepForward(Tensor state, Tensor? hidden = null)
    {
        using var scope = torch.NewDisposeScope();

        using var processed = ProcessState(state);
        using var input3d = processed.unsqueeze(0);

        Tensor output3d, newHidden;
        if (hidden is not null)
        {
            var (o, h) = _gru.call(input3d, hidden);
            output3d = o;
            newHidden = h;
        }
        else
        {
            var (o, h) = _gru.call(input3d);
            output3d = o;
            newHidden = h;
        }

        using var features = functional.relu(_fc.forward(output3d.squeeze(0)));

        // Action head with masking
        var actionLogits = _actorHead.forward(features);
        using var actionMask = BuildActionMask(state);
        var maskedLogits = actionLogits + (1 - actionMask) * (-1e9f);
        var actionProbs = functional.softmax(maskedLogits, dim: -1);
        using var actions2d = multinomial(actionProbs, 1);
        var actions = actions2d.squeeze(1);

        var actionLogProbsFull = log(actionProbs + 1e-8f);
        var actionLogProbs = actionLogProbsFull.gather(1, actions.unsqueeze(1)).squeeze(1);
        var actionEntropy = -(actionProbs * actionLogProbsFull).sum(1);

        // Chemical head: continuous [0,1] via sigmoid-gaussian
        // Output logit → sigmoid → mean in (0,1). Fixed std=0.15 for exploration.
        using var chemLogit = _chemicalHead.forward(features);
        using var chemMean = torch.sigmoid(chemLogit).squeeze(-1); // [N]
        const float chemStd = 0.15f;
        using var chemNoise = torch.randn(chemMean.shape, device: chemMean.device) * chemStd;
        using var chemRaw = chemMean + chemNoise;
        var chemicalValues = chemRaw.clamp(0f, 1f); // [N]

        // Gaussian log prob
        using var chemDiff = chemicalValues - chemMean;
        using var chemLogStd = torch.tensor(MathF.Log(chemStd), device: chemMean.device);
        var chemLogProbs = -0.5f * (chemDiff * chemDiff / (chemStd * chemStd) + 2f * chemLogStd + MathF.Log(2f * MathF.PI));
        var chemEntropy = 0.5f * (1f + MathF.Log(2f * MathF.PI)) + chemLogStd;

        // Combined log prob: action_lp + chemical_lp (when action=EmitChemical)
        using var isEmitChem = actions.eq((long)ZhiAction.EmitChemical).@float();
        var logProbs = actionLogProbs + isEmitChem * chemLogProbs;
        var entropy = actionEntropy + isEmitChem * chemEntropy;

        var values = _criticHead.forward(features).squeeze(-1);

        return (actions.MoveToOuterDisposeScope(),
                chemicalValues.MoveToOuterDisposeScope(),
                logProbs.MoveToOuterDisposeScope(),
                values.MoveToOuterDisposeScope(),
                entropy.MoveToOuterDisposeScope(),
                newHidden.MoveToOuterDisposeScope());
    }

    /// <summary>
    /// Sequence forward for PPO. stateSeq: [T, N, S], actions: [T, N], chemicalValues: [T, N].
    /// </summary>
    public (Tensor logProbs, Tensor values, Tensor entropy)
        SeqForward(Tensor stateSeq, Tensor actions, Tensor chemicalValues, Tensor? initHidden = null)
    {
        using var scope = torch.NewDisposeScope();

        int T = (int)stateSeq.shape[0];
        int N = (int)stateSeq.shape[1];

        using var flatStates = stateSeq.reshape([T * N, ToolDefinitions.StateSize]);
        using var processed = ProcessState(flatStates);
        using var processedSeq = processed.reshape([T, N, _gruInputSize]);

        Tensor output3d;
        if (initHidden is not null)
        {
            var (o, _) = _gru.call(processedSeq, initHidden);
            output3d = o;
        }
        else
        {
            var (o, _) = _gru.call(processedSeq);
            output3d = o;
        }

        using var flat = output3d.reshape([-1, _hiddenSize]);
        using var features = functional.relu(_fc.forward(flat));

        // Action head
        var actionLogits = _actorHead.forward(features);
        using var actionMask = BuildActionMask(flatStates);
        var maskedLogits = actionLogits + (1 - actionMask) * (-1e9f);
        var actionProbs = functional.softmax(maskedLogits, dim: -1);
        var actionLogProbsFull = log(actionProbs + 1e-8f);
        var flatActions = actions.reshape([-1]);
        var actionLogProbs = actionLogProbsFull.gather(1, flatActions.unsqueeze(1)).squeeze(1);
        var actionEntropy = -(actionProbs * actionLogProbsFull).sum(1);

        // Chemical head: gaussian log prob for continuous [0,1] values
        using var chemLogit = _chemicalHead.forward(features);
        using var chemMean = torch.sigmoid(chemLogit).squeeze(-1);
        const float chemStd = 0.15f;
        var flatChemValues = chemicalValues.reshape([-1]);
        using var chemDiff = flatChemValues - chemMean;
        using var chemLogStd = torch.tensor(MathF.Log(chemStd), device: chemMean.device);
        var chemLogProbs = -0.5f * (chemDiff * chemDiff / (chemStd * chemStd) + 2f * chemLogStd + MathF.Log(2f * MathF.PI));
        var chemEntropy = 0.5f * (1f + MathF.Log(2f * MathF.PI)) + chemLogStd;

        // Combine
        using var isEmitChem = flatActions.eq((long)ZhiAction.EmitChemical).@float();
        var logProbs = actionLogProbs + isEmitChem * chemLogProbs;
        var entropy = actionEntropy + isEmitChem * chemEntropy;

        var values = _criticHead.forward(features).squeeze(-1);

        return (logProbs.reshape([T, N]).MoveToOuterDisposeScope(),
                values.reshape([T, N]).MoveToOuterDisposeScope(),
                entropy.reshape([T, N]).MoveToOuterDisposeScope());
    }

    public float PpoUpdate(
        Tensor stateSeq,
        Tensor actions,
        Tensor chemicalValues,
        Tensor oldLogProbs,
        Tensor advantages,
        Tensor returns,
        int epochs = 4,
        float clipEpsilon = 0.2f,
        float entCoef = 0.01f)
    {
        _optimizer?.Dispose();
        _optimizer = optim.Adam(parameters(), _learningRate);

        float totalLoss = 0f;
        int updates = 0;

        using var advMean = advantages.mean();
        using var advStd = advantages.std() + 1e-8f;
        using var advNorm = (advantages - advMean) / advStd;

        for (int epoch = 0; epoch < epochs; epoch++)
        {
            var (newLogProbs, values, entropy) = SeqForward(stateSeq, actions, chemicalValues);

            using var ratio = exp(newLogProbs - oldLogProbs);
            using var surr1 = ratio * advNorm;
            using var surr2 = ratio.clamp(1f - clipEpsilon, 1f + clipEpsilon) * advNorm;
            using var actorLoss = -minimum(surr1, surr2).mean();
            using var valueLoss = functional.mse_loss(values, returns);
            using var entMean = entropy.mean();
            using var loss = actorLoss + 0.5f * valueLoss - entCoef * entMean;

            _optimizer.zero_grad();
            loss.backward();
            _optimizer.step();

            totalLoss += loss.item<float>();
            updates++;

            newLogProbs.Dispose();
            values.Dispose();
            entropy.Dispose();
        }

        return totalLoss / updates;
    }

    public byte[] SaveWeights()
    {
        this.to(CPU);
        using var ms = new MemoryStream();
        save(ms);
        this.to(Device.TorchDevice);
        return ms.ToArray();
    }

    public void LoadWeights(byte[] weights)
    {
        using var ms = new MemoryStream(weights);
        load(ms);
        this.to(Device.TorchDevice);
        _optimizer?.Dispose();
        _optimizer = optim.Adam(parameters(), _learningRate);
    }
}
