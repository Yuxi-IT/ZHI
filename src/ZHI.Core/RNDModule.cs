using TorchSharp;
using TorchSharp.Modules;
using ZHI.Shared;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace ZHI.Core;

/// <summary>
/// Random Network Distillation — intrinsic curiosity reward.
/// Target (fixed random) vs Predictor (trained). Prediction error → exploration bonus.
/// </summary>
public class RNDModule : IDisposable
{
    private readonly Sequential _target;
    private readonly Sequential _predictor;
    private readonly optim.Optimizer _optimizer;
    private readonly float _rewardScale;

    /// <summary>
    /// Builds a small embedding network: 21→64→32→16
    /// </summary>
    private static Sequential BuildEmbedding(string name)
    {
        return Sequential(
            ("fc1_" + name, Linear(ToolDefinitions.StateSize, 64)),
            ("relu1_" + name, ReLU()),
            ("fc2_" + name, Linear(64, 32)),
            ("relu2_" + name, ReLU()),
            ("fc3_" + name, Linear(32, 16))
        );
    }

    public RNDModule(float learningRate = 0.0005f, float rewardScale = 0.1f)
    {
        _rewardScale = rewardScale;

        _target = BuildEmbedding("target");
        _predictor = BuildEmbedding("predictor");

        // Initialize with Xavier-like random (default Linear init is fine for this)
        // Move to GPU
        _target.to(Device.TorchDevice);
        _predictor.to(Device.TorchDevice);

        // Target is FROZEN — no optimizer, no training
        foreach (var (_, p) in _target.named_parameters())
            p.requires_grad = false;

        // Only predictor is trained
        _optimizer = optim.Adam(_predictor.parameters(), learningRate);
    }

    /// <summary>
    /// Compute intrinsic reward for batch of states [N, 21].
    /// Returns rewards [N] — higher for novel/unfamiliar states.
    /// </summary>
    public Tensor ComputeIntrinsicReward(Tensor states)
    {
        using var scope = torch.NewDisposeScope();

        using var targetOut = _target.forward(states);     // [N, 16]
        using var predOut = _predictor.forward(states);     // [N, 16]
        using var error = targetOut - predOut;
        using var mse = (error * error).mean(new long[] { 1 });             // [N] per-sample MSE
        var reward = mse * _rewardScale;

        return reward.MoveToOuterDisposeScope();
    }

    /// <summary>
    /// Train predictor to better match target (reduce prediction error).
    /// </summary>
    public float Train(Tensor states)
    {
        using var scope = torch.NewDisposeScope();

        using var targetOut = _target.forward(states);
        var predOut = _predictor.forward(states);
        using var loss = functional.mse_loss(predOut, targetOut);

        _optimizer.zero_grad();
        loss.backward();
        _optimizer.step();

        var lossVal = loss.item<float>();
        predOut.Dispose();
        scope.Dispose();
        return lossVal;
    }

    public byte[] SaveWeights()
    {
        _predictor.to(CPU);
        using var ms = new MemoryStream();
        _predictor.save(ms);
        _predictor.to(Device.TorchDevice);
        return ms.ToArray();
    }

    public void LoadWeights(byte[] weights)
    {
        using var ms = new MemoryStream(weights);
        _predictor.load(ms);
        _predictor.to(Device.TorchDevice);
    }

    public void Dispose()
    {
        _target.Dispose();
        _predictor.Dispose();
        _optimizer.Dispose();
    }
}
