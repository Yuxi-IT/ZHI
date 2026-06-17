using TorchSharp;
using ZHI.Shared;
using static TorchSharp.torch;

namespace ZHI.Watcher;

public struct FoodTile
{
    public int X;
    public int Y;
    public int TTL;
}

/// <summary>
/// Spatial grid world state for 20×20 ecosystem.
/// Agents have positions, existence, stress. Food tiles have TTL.
/// Scent grid tracks movement trails.
/// </summary>
public class VectorizedState : IDisposable
{
    public int N { get; }
    public Device Device { get; }

    // Agent state (CPU arrays — spatial logic is CPU-side)
    public int[] PosX;
    public int[] PosY;
    public float[] Existence;
    public float[] Stress;
    public bool[] Alive;
    public long[] LastAction;
    public int[] LastSignalReceived; // -1 = none
    public int[] TickCount;
    public int[] AttackCount;
    public int[] EatCount;
    public int[] SignalCount;
    public string[] StatusMirror;
    public string[] LastActionNameMirror;
    public DateTime[] BirthTimes;

    // Grid state
    public List<FoodTile> FoodTiles;
    public float[,] ScentGrid; // [GridWidth, GridHeight]

    // GPU tensor for batch inference
    public Tensor StateMatrix;

    // Pre-allocated buffer for state assembly
    private readonly float[] _stateAssemblyBuffer;

    public VectorizedState(int n, torch.Device device)
    {
        N = n;
        Device = device;

        PosX = new int[n];
        PosY = new int[n];
        Existence = new float[n];
        Stress = new float[n];
        Alive = new bool[n];
        LastAction = new long[n];
        LastSignalReceived = new int[n];
        TickCount = new int[n];
        AttackCount = new int[n];
        EatCount = new int[n];
        SignalCount = new int[n];
        StatusMirror = new string[n];
        LastActionNameMirror = new string[n];
        BirthTimes = new DateTime[n];

        FoodTiles = new List<FoodTile>();
        ScentGrid = new float[ToolDefinitions.GridWidth, ToolDefinitions.GridHeight];

        StateMatrix = torch.zeros(n, ToolDefinitions.StateSize, device: device);
        _stateAssemblyBuffer = new float[n * ToolDefinitions.StateSize];

        var rng = new Random();
        for (int i = 0; i < n; i++)
        {
            PosX[i] = rng.Next(ToolDefinitions.GridWidth);
            PosY[i] = rng.Next(ToolDefinitions.GridHeight);
            Existence[i] = 100f;
            Stress[i] = 0f;
            Alive[i] = true;
            LastAction[i] = 0;
            LastSignalReceived[i] = -1;
            TickCount[i] = 0;
            AttackCount[i] = 0;
            EatCount[i] = 0;
            SignalCount[i] = 0;
            StatusMirror[i] = "idle";
            LastActionNameMirror[i] = "none";
            BirthTimes[i] = DateTime.UtcNow;
        }
    }

    public void BuildStateMatrix()
    {
        int S = ToolDefinitions.StateSize;
        int R = ToolDefinitions.VisionRadius;
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;

        Array.Clear(_stateAssemblyBuffer);

        for (int i = 0; i < N; i++)
        {
            if (!Alive[i]) continue;

            int baseIdx = i * S;
            int cx = PosX[i];
            int cy = PosY[i];

            // [0-24] 5×5 local grid
            for (int dy = -R; dy <= R; dy++)
            {
                for (int dx = -R; dx <= R; dx++)
                {
                    int gx = cx + dx;
                    int gy = cy + dy;
                    int cellIdx = (dy + R) * 5 + (dx + R);

                    if (gx < 0 || gx >= W || gy < 0 || gy >= H)
                    {
                        _stateAssemblyBuffer[baseIdx + cellIdx] = 0f;
                        continue;
                    }

                    if (dx == 0 && dy == 0)
                    {
                        _stateAssemblyBuffer[baseIdx + cellIdx] = 1.0f;
                        continue;
                    }

                    bool hasFood = HasFoodAt(gx, gy);
                    bool hasAgent = HasOtherAgentAt(i, gx, gy);

                    float val = 0f;
                    if (hasAgent && hasFood) val = 0.8f;
                    else if (hasAgent) val = 0.5f;
                    else if (hasFood) val = 0.2f;

                    _stateAssemblyBuffer[baseIdx + cellIdx] = val;
                }
            }

            // [25-29] self state
            _stateAssemblyBuffer[baseIdx + 25] = Existence[i] / 100f;
            _stateAssemblyBuffer[baseIdx + 26] = Stress[i] / 5f;
            _stateAssemblyBuffer[baseIdx + 27] = LastAction[i] / 6f;
            _stateAssemblyBuffer[baseIdx + 28] = Math.Min(TickCount[i] / 200f, 1f);
            _stateAssemblyBuffer[baseIdx + 29] = LastSignalReceived[i] >= 0
                ? LastSignalReceived[i] / 3f
                : 0f;

            // [30-33] scent gradient
            float scentHere = ScentGrid[cx, cy];
            float sNorth = (cy > 0) ? ScentGrid[cx, cy - 1] - scentHere : 0f;
            float sSouth = (cy < H - 1) ? ScentGrid[cx, cy + 1] - scentHere : 0f;
            float sEast = (cx < W - 1) ? ScentGrid[cx + 1, cy] - scentHere : 0f;
            float sWest = (cx > 0) ? ScentGrid[cx - 1, cy] - scentHere : 0f;

            float maxScent = Math.Max(1f, Math.Max(Math.Abs(sNorth),
                Math.Max(Math.Abs(sSouth), Math.Max(Math.Abs(sEast), Math.Abs(sWest)))));
            _stateAssemblyBuffer[baseIdx + 30] = sNorth / maxScent;
            _stateAssemblyBuffer[baseIdx + 31] = sSouth / maxScent;
            _stateAssemblyBuffer[baseIdx + 32] = sEast / maxScent;
            _stateAssemblyBuffer[baseIdx + 33] = sWest / maxScent;

            // [34-36] local stats
            int foodVisible = 0;
            int agentVisible = 0;
            for (int dy = -R; dy <= R; dy++)
            {
                for (int dx = -R; dx <= R; dx++)
                {
                    int gx = cx + dx;
                    int gy = cy + dy;
                    if (gx < 0 || gx >= W || gy < 0 || gy >= H) continue;
                    if (HasFoodAt(gx, gy)) foodVisible++;
                    if (dx == 0 && dy == 0) continue;
                    if (HasOtherAgentAt(i, gx, gy)) agentVisible++;
                }
            }
            _stateAssemblyBuffer[baseIdx + 34] = Math.Min(foodVisible / 5f, 1f);
            _stateAssemblyBuffer[baseIdx + 35] = Math.Min(agentVisible / 8f, 1f);
            _stateAssemblyBuffer[baseIdx + 36] = Math.Min(scentHere / 10f, 1f);
        }

        StateMatrix.Dispose();
        StateMatrix = tensor(_stateAssemblyBuffer, [N, S], device: Device);
    }

    private bool HasFoodAt(int x, int y)
    {
        for (int i = 0; i < FoodTiles.Count; i++)
        {
            if (FoodTiles[i].X == x && FoodTiles[i].Y == y)
                return true;
        }
        return false;
    }

    private bool HasOtherAgentAt(int excludeIdx, int x, int y)
    {
        for (int i = 0; i < N; i++)
        {
            if (i == excludeIdx || !Alive[i]) continue;
            if (PosX[i] == x && PosY[i] == y) return true;
        }
        return false;
    }

    public float[] GetStateBuffer() => _stateAssemblyBuffer;

    public void Dispose()
    {
        StateMatrix?.Dispose();
    }
}
