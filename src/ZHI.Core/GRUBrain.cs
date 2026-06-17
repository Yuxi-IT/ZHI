using TorchSharp;
using TorchSharp.Modules;
using ZHI.Shared;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace ZHI.Core;

/// <summary>
/// GRU-based Actor-Critic with dual-head policy.
/// Action head: 8 actions (Move×4, Eat, Attack, Signal, Hide)
/// Signal head: 4 signal values (only used when action=Signal)
/// Architecture: GRU(44→128) → Linear(128→64) → Actor(64→8) + Signal(64→4) / Critic(64→1)
/// </summary>
public class GRUBrain : Module
{
    private readonly GRU _gru;
    private readonly Linear _fc;
    private readonly Linear _actorHead;
    private readonly Linear _signalHead;
    private readonly Linear _criticHead;

    private optim.Optimizer? _optimizer;
    private readonly float _gamma;
    private readonly float _learningRate;
    private readonly int _hiddenSize;

    public int HiddenSize => _hiddenSize;

    public GRUBrain() : base("GRUBrain")
    {
        _hiddenSize = 128;
        _gru = GRU(ToolDefinitions.StateSize, _hiddenSize, 1, batchFirst: false);
        _fc = Linear(_hiddenSize, 64);
        _actorHead = Linear(64, ToolDefinitions.ActionCount);
        _signalHead = Linear(64, ToolDefinitions.SignalValues);
        _criticHead = Linear(64, 1);
        _gamma = 0.99f;
        _learningRate = 0.0003f;

        RegisterComponents();
        this.to(Device.TorchDevice);
    }

    public GRUBrain(float learningRate, float gamma) : base("GRUBrain")
    {
        _hiddenSize = 128;
        _gru = GRU(ToolDefinitions.StateSize, _hiddenSize, 1, batchFirst: false);
        _fc = Linear(_hiddenSize, 64);
        _actorHead = Linear(64, ToolDefinitions.ActionCount);
        _signalHead = Linear(64, ToolDefinitions.SignalValues);
        _criticHead = Linear(64, 1);
        _gamma = gamma;
        _learningRate = learningRate;

        RegisterComponents();
        this.to(Device.TorchDevice);
    }

    /// <summary>
    /// Build action mask from state tensor. Hiding agents can only Signal(6) or Hide(7).
    /// Returns [N, ActionCount] tensor with 1=valid, 0=invalid.
    /// </summary>
    private Tensor BuildActionMask(Tensor states)
    {
        int N = (int)states.shape[0];
        int S = ToolDefinitions.StateSize;
        int A = ToolDefinitions.ActionCount;
        float[] maskData = new float[N * A];

        var stateData = states.data<float>();

        for (int i = 0; i < N; i++)
        {
            bool isHiding = stateData[i * S + 140] > 0.5f;
            float hunger = stateData[i * S + 144]; // normalized 0-1
            float thirst = stateData[i * S + 145]; // normalized 0-1
            bool critical = hunger < 0.3f || thirst < 0.3f;
            int b = i * A;

            // Default: all actions valid
            for (int a = 0; a < A; a++) maskData[b + a] = 1f;

            if (isHiding && !critical)
            {
                // Mask: Move(0-3), Eat(4), Attack(5), Drink(8)
                // Keep: Signal(6), Hide(7)
                maskData[b + 0] = 0f; maskData[b + 1] = 0f;
                maskData[b + 2] = 0f; maskData[b + 3] = 0f;
                maskData[b + 4] = 0f; maskData[b + 5] = 0f;
                maskData[b + 8] = 0f;
            }
        }

        return torch.tensor(maskData, [N, A]).to(states.device);
    }

    /// <summary>
    /// Single-step forward with dual heads and action masking.
    /// Returns (actions[N], signalValues[N], logProbs[N], values[N], newHidden)
    /// logProbs includes both action + signal log prob (summed when action=Signal).
    /// </summary>
    public (Tensor actions, Tensor signalValues, Tensor logProbs, Tensor values, Tensor entropy, Tensor newHidden)
        StepForward(Tensor state, Tensor? hidden = null)
    {
        using var scope = torch.NewDisposeScope();

        using var input3d = state.unsqueeze(0); // [1, N, S]

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

        using var features = functional.relu(_fc.forward(output3d.squeeze(0))); // [N, 64]

        // Action head with masking
        var actionLogits = _actorHead.forward(features);
        using var actionMask = BuildActionMask(state);
        var maskedLogits = actionLogits + (1 - actionMask) * (-1e9f);
        var actionProbs = functional.softmax(maskedLogits, dim: -1);
        using var actions2d = multinomial(actionProbs, 1);
        var actions = actions2d.squeeze(1); // [N]

        var actionLogProbsFull = log(actionProbs + 1e-8f);
        var actionLogProbs = actionLogProbsFull.gather(1, actions.unsqueeze(1)).squeeze(1); // [N]
        var actionEntropy = -(actionProbs * actionLogProbsFull).sum(1);

        // Signal head
        var signalLogits = _signalHead.forward(features);
        var signalProbs = functional.softmax(signalLogits, dim: -1);
        using var signalValues2d = multinomial(signalProbs, 1);
        var signalValues = signalValues2d.squeeze(1); // [N]

        var signalLogProbsFull = log(signalProbs + 1e-8f);
        var signalLogProbs = signalLogProbsFull.gather(1, signalValues.unsqueeze(1)).squeeze(1);
        var signalEntropy = -(signalProbs * signalLogProbsFull).sum(1);

        // Combined log prob: action_lp + signal_lp (when action=Signal)
        using var isSignal = actions.eq((long)ZhiAction.Signal).@float();
        var logProbs = actionLogProbs + isSignal * signalLogProbs;
        var entropy = actionEntropy + isSignal * signalEntropy;

        // Critic
        var values = _criticHead.forward(features).squeeze(-1);

        return (actions.MoveToOuterDisposeScope(),
                signalValues.MoveToOuterDisposeScope(),
                logProbs.MoveToOuterDisposeScope(),
                values.MoveToOuterDisposeScope(),
                entropy.MoveToOuterDisposeScope(),
                newHidden.MoveToOuterDisposeScope());
    }

    /// <summary>
    /// Sequence forward for PPO with action masking. stateSeq: [T, N, S], actions: [T, N], signalValues: [T, N].
    /// </summary>
    public (Tensor logProbs, Tensor values, Tensor entropy)
        SeqForward(Tensor stateSeq, Tensor actions, Tensor signalValues, Tensor? initHidden = null)
    {
        using var scope = torch.NewDisposeScope();

        Tensor output3d;
        if (initHidden is not null)
        {
            var (o, _) = _gru.call(stateSeq, initHidden);
            output3d = o;
        }
        else
        {
            var (o, _) = _gru.call(stateSeq);
            output3d = o;
        }

        using var flat = output3d.reshape([-1, _hiddenSize]);
        using var features = functional.relu(_fc.forward(flat));

        // Action head with masking
        var actionLogits = _actorHead.forward(features);
        var flatStates = stateSeq.reshape([-1, ToolDefinitions.StateSize]);
        using var actionMask = BuildActionMask(flatStates);
        var maskedLogits = actionLogits + (1 - actionMask) * (-1e9f);
        var actionProbs = functional.softmax(maskedLogits, dim: -1);
        var actionLogProbsFull = log(actionProbs + 1e-8f);
        var flatActions = actions.reshape([-1]);
        var actionLogProbs = actionLogProbsFull.gather(1, flatActions.unsqueeze(1)).squeeze(1);
        var actionEntropy = -(actionProbs * actionLogProbsFull).sum(1);

        // Signal head
        var signalLogits = _signalHead.forward(features);
        var signalProbs = functional.softmax(signalLogits, dim: -1);
        var signalLogProbsFull = log(signalProbs + 1e-8f);
        var flatSignals = signalValues.reshape([-1]);
        var signalLogProbs = signalLogProbsFull.gather(1, flatSignals.unsqueeze(1)).squeeze(1);
        var signalEntropy = -(signalProbs * signalLogProbsFull).sum(1);

        // Combine
        using var isSignal = flatActions.eq((long)ZhiAction.Signal).@float();
        var logProbs = actionLogProbs + isSignal * signalLogProbs;
        var entropy = actionEntropy + isSignal * signalEntropy;

        var values = _criticHead.forward(features).squeeze(-1);

        int T = (int)stateSeq.shape[0];
        int N = (int)stateSeq.shape[1];

        return (logProbs.reshape([T, N]).MoveToOuterDisposeScope(),
                values.reshape([T, N]).MoveToOuterDisposeScope(),
                entropy.reshape([T, N]).MoveToOuterDisposeScope());
    }

    /// <summary>PPO update with dual-head policy.</summary>
    public float PpoUpdate(
        Tensor stateSeq,
        Tensor actions,
        Tensor signalValues,
        Tensor oldLogProbs,
        Tensor advantages,
        Tensor returns,
        int epochs = 4,
        float clipEpsilon = 0.2f,
        float entCoef = 0.01f)
    {
        // Create fresh optimizer each PPO update to avoid stale parameter handles
        _optimizer?.Dispose();
        _optimizer = optim.Adam(parameters(), _learningRate);

        float totalLoss = 0f;
        int updates = 0;

        using var advMean = advantages.mean();
        using var advStd = advantages.std() + 1e-8f;
        using var advNorm = (advantages - advMean) / advStd;

        for (int epoch = 0; epoch < epochs; epoch++)
        {
            var (newLogProbs, values, entropy) = SeqForward(stateSeq, actions, signalValues);

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
