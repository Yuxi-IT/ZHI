using TorchSharp;
using ZHI.Shared;
using static TorchSharp.torch;

namespace ZHI.Watcher;

/// <summary>
/// PPO Rollout Buffer — stores T timesteps of N-agent experience on GPU.
/// </summary>
public class PPOBuffer : IDisposable
{
    private readonly int _maxSteps;
    private readonly int _numAgents;
    private readonly int _stateSize;
    private readonly torch.Device _device;

    // GPU tensor storage (pre-allocated, shape [T, N, ...])
    private Tensor _gpuStates;       // [T, N, S]
    private Tensor _gpuActions;      // [T, N] int64
    private Tensor _gpuSignals;      // [T, N] int64
    private Tensor _gpuLogProbs;     // [T, N]
    private Tensor _gpuRewards;      // [T, N]
    private Tensor _gpuDones;        // [T, N]
    private Tensor _gpuValues;       // [T, N]

    public int Count { get; private set; }
    public bool IsFull => Count >= _maxSteps;

    public PPOBuffer(int maxSteps, int numAgents, torch.Device device)
    {
        _maxSteps = maxSteps;
        _numAgents = numAgents;
        _stateSize = ToolDefinitions.StateSize;
        _device = device;

        int T = maxSteps;
        int N = numAgents;
        _gpuStates = zeros([T, N, _stateSize], device: device);
        _gpuActions = zeros([T, N], dtype: int64, device: device);
        _gpuSignals = zeros([T, N], dtype: int64, device: device);
        _gpuLogProbs = zeros([T, N], device: device);
        _gpuRewards = zeros([T, N], device: device);
        _gpuDones = zeros([T, N], device: device);
        _gpuValues = zeros([T, N], device: device);
    }

    /// <summary>
    /// Store one timestep of experience from all agents. Data copied from CPU to GPU slice.
    /// </summary>
    public void Store(
        float[] states,       // [N * stateSize]
        long[] actions,       // [N]
        long[] signalValues,  // [N]
        float[] logProbs,     // [N]
        float[] rewards,      // [N]
        float[] dones,        // [N]
        float[] values)       // [N]
    {
        if (Count >= _maxSteps) return;

        int t = Count;
        using var scope = NewDisposeScope();

        using var cpuS = tensor(states, [_numAgents, _stateSize]);
        using var cpuA = tensor(actions, [_numAgents], dtype: int64);
        using var cpuSig = tensor(signalValues, [_numAgents], dtype: int64);
        using var cpuLp = tensor(logProbs, [_numAgents]);
        using var cpuR = tensor(rewards, [_numAgents]);
        using var cpuD = tensor(dones, [_numAgents]);
        using var cpuV = tensor(values, [_numAgents]);

        _gpuStates[t].copy_(cpuS);
        _gpuActions[t].copy_(cpuA);
        _gpuSignals[t].copy_(cpuSig);
        _gpuLogProbs[t].copy_(cpuLp);
        _gpuRewards[t].copy_(cpuR);
        _gpuDones[t].copy_(cpuD);
        _gpuValues[t].copy_(cpuV);

        Count++;
    }

    /// <summary>
    /// Compute GAE advantages and returns. Downloads small arrays from GPU, computes on CPU.
    /// </summary>
    public (float[] advantages, float[] returns) ComputeGAE(float gamma, float lambda)
    {
        int total = Count * _numAgents;

        // Download rewards / values / dones from GPU to CPU (each ~16 KB for T=64 N=64)
        var rews = new float[total];
        var vals = new float[total];
        var dones = new float[total];
        using (var cpuR = _gpuRewards.cpu()) cpuR.data<float>().CopyTo(rews);
        using (var cpuV = _gpuValues.cpu()) cpuV.data<float>().CopyTo(vals);
        using (var cpuD = _gpuDones.cpu()) cpuD.data<float>().CopyTo(dones);

        var advantages = new float[total];
        var returnsArr = new float[total];

        for (int a = 0; a < _numAgents; a++)
        {
            float gae = 0f;
            for (int t = Count - 1; t >= 0; t--)
            {
                int idx = t * _numAgents + a;
                int nextIdx = (t + 1) * _numAgents + a;

                float nextValue = (t < Count - 1) ? vals[nextIdx] : 0f;
                float done = dones[idx];

                float delta = rews[idx] + gamma * nextValue * (1f - done) - vals[idx];
                gae = delta + gamma * lambda * (1f - done) * gae;
                advantages[idx] = gae;
                returnsArr[idx] = gae + vals[idx];
            }
        }

        return (advantages, returnsArr);
    }

    /// <summary>
    /// Convert stored experience to GPU tensors for training. Returns null if empty.
    /// Uploads advantages/returns (computed by CPU-side GAE) to GPU.
    /// </summary>
    public (Tensor states, Tensor actions, Tensor signalValues, Tensor oldLogProbs, Tensor advantages, Tensor returns)?
        ToTensors(float[] advantages, float[] returnsArr)
    {
        if (Count == 0) return null;

        int T = Count;
        int N = _numAgents;

        // Clone GPU-stored tensors (GPU-to-GPU copy, no PCIe transfer) + upload advantages/returns
        return (
            _gpuStates.narrow(0, 0, T).clone(),
            _gpuActions.narrow(0, 0, T).clone(),
            _gpuSignals.narrow(0, 0, T).clone(),
            _gpuLogProbs.narrow(0, 0, T).clone(),
            tensor(advantages, [T, N], device: _device),
            tensor(returnsArr, [T, N], device: _device)
        );
    }

    public void Clear()
    {
        Count = 0;
    }

    public void Dispose()
    {
        _gpuStates?.Dispose();
        _gpuActions?.Dispose();
        _gpuSignals?.Dispose();
        _gpuLogProbs?.Dispose();
        _gpuRewards?.Dispose();
        _gpuDones?.Dispose();
        _gpuValues?.Dispose();
    }
}
