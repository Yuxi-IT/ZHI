using TorchSharp;
using ZHI.Shared;
using static TorchSharp.torch;

namespace ZHI.Watcher;

public class PPOBuffer : IDisposable
{
    private readonly int _maxSteps;
    private readonly int _numAgents;
    private readonly int _stateSize;
    private readonly torch.Device _device;

    private Tensor _gpuStates;
    private Tensor _gpuActions;
    private Tensor _gpuChemicalValues;  // [T, N] float32 — continuous emission 0–1
    private Tensor _gpuLogProbs;
    private Tensor _gpuRewards;
    private Tensor _gpuDones;
    private Tensor _gpuValues;

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
        _gpuChemicalValues = zeros([T, N], device: device);
        _gpuLogProbs = zeros([T, N], device: device);
        _gpuRewards = zeros([T, N], device: device);
        _gpuDones = zeros([T, N], device: device);
        _gpuValues = zeros([T, N], device: device);
    }

    public void Store(
        float[] states,
        long[] actions,
        float[] chemicalValues,
        float[] logProbs,
        float[] rewards,
        float[] dones,
        float[] values)
    {
        if (Count >= _maxSteps) return;

        int t = Count;
        using var scope = NewDisposeScope();

        using var cpuS = tensor(states, [_numAgents, _stateSize]);
        using var cpuA = tensor(actions, [_numAgents], dtype: int64);
        using var cpuChem = tensor(chemicalValues, [_numAgents]);
        using var cpuLp = tensor(logProbs, [_numAgents]);
        using var cpuR = tensor(rewards, [_numAgents]);
        using var cpuD = tensor(dones, [_numAgents]);
        using var cpuV = tensor(values, [_numAgents]);

        _gpuStates[t].copy_(cpuS);
        _gpuActions[t].copy_(cpuA);
        _gpuChemicalValues[t].copy_(cpuChem);
        _gpuLogProbs[t].copy_(cpuLp);
        _gpuRewards[t].copy_(cpuR);
        _gpuDones[t].copy_(cpuD);
        _gpuValues[t].copy_(cpuV);

        Count++;
    }

    public (float[] advantages, float[] returns) ComputeGAE(float gamma, float lambda)
    {
        int total = Count * _numAgents;

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

    public (Tensor states, Tensor actions, Tensor chemicalValues, Tensor oldLogProbs, Tensor advantages, Tensor returns)?
        ToTensors(float[] advantages, float[] returnsArr)
    {
        if (Count == 0) return null;

        int T = Count;
        int N = _numAgents;

        return (
            _gpuStates.narrow(0, 0, T).clone(),
            _gpuActions.narrow(0, 0, T).clone(),
            _gpuChemicalValues.narrow(0, 0, T).clone(),
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
        _gpuChemicalValues?.Dispose();
        _gpuLogProbs?.Dispose();
        _gpuRewards?.Dispose();
        _gpuDones?.Dispose();
        _gpuValues?.Dispose();
    }
}
