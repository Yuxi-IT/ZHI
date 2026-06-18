using ZHI.Shared;
using ZHI.Watcher;

namespace ZHI.Tests;

public class PPOBufferTests
{
    [Fact]
    public void Store_OneStep_IncrementsCount()
    {
        // Use CPU device for tests
        var device = torch.CPU;
        var buffer = new PPOBuffer(maxSteps: 4, numAgents: 2, device);

        int s = ToolDefinitions.StateSize;
        var states = new float[2 * s];
        var actions = new long[] { 0, 1 };
        var signals = new long[] { 0, 0 };
        var logProbs = new float[] { -0.5f, -0.3f };
        var rewards = new float[] { 0.1f, -0.2f };
        var dones = new float[] { 0f, 0f };
        var values = new float[] { 1.0f, 2.0f };

        buffer.Store(states, actions, signals, logProbs, rewards, dones, values);
        Assert.Equal(1, buffer.Count);
        Assert.False(buffer.IsFull);

        buffer.Dispose();
    }

    [Fact]
    public void Store_Full_IsFullReturnsTrue()
    {
        var device = torch.CPU;
        int T = 2, N = 2;
        var buffer = new PPOBuffer(maxSteps: T, numAgents: N, device);

        int s = ToolDefinitions.StateSize;
        for (int t = 0; t < T; t++)
        {
            var states = new float[N * s];
            buffer.Store(states, new long[N], new long[N],
                new float[N], new float[N], new float[N], new float[N]);
        }

        Assert.True(buffer.IsFull);

        buffer.Dispose();
    }

    [Fact]
    public void ComputeGAE_KnownValues_ProducesExpectedResult()
    {
        var device = torch.CPU;
        int T = 2, N = 1;
        var buffer = new PPOBuffer(maxSteps: T, numAgents: N, device);

        int s = ToolDefinitions.StateSize;
        // Step 0: r=1, v=0, done=0
        buffer.Store(new float[N * s], [0], [0], [0f], [1f], [0f], [0f]);
        // Step 1: r=1, v=1, done=1
        buffer.Store(new float[N * s], [0], [0], [0f], [1f], [1f], [1f]);

        Assert.True(buffer.IsFull);

        float gamma = 0.99f, lambda = 0.95f;
        var (advantages, returns) = buffer.ComputeGAE(gamma, lambda);

        // Manual GAE for step 1: delta = 1 + 0.99*0*(1-1) - 1 = 0, gae = 0, ret = 0 + 1 = 1
        Assert.Equal(0f, advantages[1], 0.001f);
        Assert.Equal(1f, returns[1], 0.001f);

        // Step 0: delta = 1 + 0.99*1*(1-0) - 0 = 1.99
        // gae = 1.99 + 0.99*0.95*(1-0)*0 = 1.99
        Assert.Equal(1.99f, advantages[0], 0.01f);
        Assert.Equal(1.99f, returns[0], 0.01f);

        buffer.Dispose();
    }

    [Fact]
    public void Clear_ResetsCount()
    {
        var device = torch.CPU;
        var buffer = new PPOBuffer(maxSteps: 4, numAgents: 2, device);

        int s = ToolDefinitions.StateSize;
        buffer.Store(new float[2 * s], new long[2], new long[2],
            new float[2], new float[2], new float[2], new float[2]);
        Assert.Equal(1, buffer.Count);

        buffer.Clear();
        Assert.Equal(0, buffer.Count);
        Assert.False(buffer.IsFull);

        buffer.Dispose();
    }

    [Fact]
    public void ToTensors_Empty_ReturnsNull()
    {
        var device = torch.CPU;
        var buffer = new PPOBuffer(maxSteps: 4, numAgents: 2, device);

        var result = buffer.ToTensors([], []);
        Assert.Null(result);

        buffer.Dispose();
    }
}
