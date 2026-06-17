using TorchSharp;
using ZHI.Core;
using ZHI.Shared;
using static TorchSharp.torch;

namespace ZHI.Watcher;

/// <summary>
/// PPO Rollout Buffer — stores T timesteps of N-agent experience on CPU.
/// Periodically flushes to GPU for batch PPO update.
/// </summary>
public class PPOBuffer : IDisposable
{
    private readonly int _maxSteps;
    private readonly int _numAgents;
    private readonly int _stateSize;
    private readonly torch.Device _device;

    // CPU storage
    private readonly float[] _states;      // [maxSteps * N * stateSize]
    private readonly long[] _actions;      // [maxSteps * N]
    private readonly long[] _signalValues; // [maxSteps * N]
    private readonly float[] _logProbs;    // [maxSteps * N]
    private readonly float[] _rewards;     // [maxSteps * N]
    private readonly float[] _dones;       // [maxSteps * N] (1=terminal, 0=ongoing)
    private readonly float[] _values;      // [maxSteps * N]

    public int Count { get; private set; }
    public int MaxSteps => _maxSteps;
    public int NumAgents => _numAgents;
    public bool IsFull => Count >= _maxSteps;

    public PPOBuffer(int maxSteps, int numAgents, torch.Device device)
    {
        _maxSteps = maxSteps;
        _numAgents = numAgents;
        _stateSize = ToolDefinitions.StateSize;
        _device = device;

        int size = maxSteps * numAgents;
        _states = new float[size * _stateSize];
        _actions = new long[size];
        _signalValues = new long[size];
        _logProbs = new float[size];
        _rewards = new float[size];
        _dones = new float[size];
        _values = new float[size];
    }

    /// <summary>
    /// Store one timestep of experience from all agents.
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

        int offset = Count * _numAgents;
        int stateOffset = offset * _stateSize;

        Array.Copy(states, 0, _states, stateOffset, _numAgents * _stateSize);
        Array.Copy(actions, 0, _actions, offset, _numAgents);
        Array.Copy(signalValues, 0, _signalValues, offset, _numAgents);
        Array.Copy(logProbs, 0, _logProbs, offset, _numAgents);
        Array.Copy(rewards, 0, _rewards, offset, _numAgents);
        Array.Copy(dones, 0, _dones, offset, _numAgents);
        Array.Copy(values, 0, _values, offset, _numAgents);

        Count++;
    }

    /// <summary>
    /// Compute GAE advantages and returns.
    /// Returns (advantages[T*N], returns[T*N]) as flat CPU arrays ready for GPU transfer.
    /// </summary>
    public (float[] advantages, float[] returns) ComputeGAE(float gamma, float lambda)
    {
        int total = Count * _numAgents;
        var advantages = new float[total];
        var returnsArr = new float[total];

        for (int a = 0; a < _numAgents; a++)
        {
            float gae = 0f;
            for (int t = Count - 1; t >= 0; t--)
            {
                int idx = t * _numAgents + a;
                int nextIdx = (t + 1) * _numAgents + a;

                float nextValue = (t < Count - 1) ? _values[nextIdx] : 0f;
                float done = _dones[idx];

                float delta = _rewards[idx] + gamma * nextValue * (1f - done) - _values[idx];
                gae = delta + gamma * lambda * (1f - done) * gae;
                advantages[idx] = gae;
                returnsArr[idx] = gae + _values[idx];
            }
        }

        return (advantages, returnsArr);
    }

    /// <summary>
    /// Convert stored experience to GPU tensors for training. Returns null if empty.
    /// </summary>
    public (Tensor states, Tensor actions, Tensor signalValues, Tensor oldLogProbs, Tensor advantages, Tensor returns)?
        ToTensors(float[] advantages, float[] returnsArr)
    {
        if (Count == 0) return null;

        int T = Count;
        int N = _numAgents;

        var statesTensor = tensor(_states, [T, N, _stateSize], device: _device);
        var actionsTensor = tensor(_actions, [T, N], dtype: int64, device: _device);
        var signalTensor = tensor(_signalValues, [T, N], dtype: int64, device: _device);
        var oldLP = tensor(_logProbs, [T, N], device: _device);
        var adv = tensor(advantages, [T, N], device: _device);
        var ret = tensor(returnsArr, [T, N], device: _device);

        return (
            statesTensor.MoveToOuterDisposeScope(),
            actionsTensor.MoveToOuterDisposeScope(),
            signalTensor.MoveToOuterDisposeScope(),
            oldLP.MoveToOuterDisposeScope(),
            adv.MoveToOuterDisposeScope(),
            ret.MoveToOuterDisposeScope()
        );
    }

    public void Clear()
    {
        Count = 0;
        Array.Clear(_states);
        Array.Clear(_actions);
        Array.Clear(_signalValues);
        Array.Clear(_logProbs);
        Array.Clear(_rewards);
        Array.Clear(_dones);
        Array.Clear(_values);
    }

    public void Dispose()
    {
        Clear();
    }
}
