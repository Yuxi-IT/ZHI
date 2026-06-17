using TorchSharp;
using ZHI.Shared;
using static TorchSharp.torch;

namespace ZHI.Watcher;

public struct FoodTile
{
    public int X;
    public int Y;
    public int Width;         // 1 for normal food, 2 for BigFood (2x2)
    public int Height;        // 1 for normal food, 2 for BigFood (2x2)
    public int TTL;
    public float Energy;
    public int EatTime;       // Ticks needed to consume (BigFood uses this)
    public bool IsBig;
}

public struct CorpseTile
{
    public int X;
    public int Y;
    public int TTL;
    public float Energy;
}

/// <summary>
/// Spatial grid world state for 128×128 ecosystem.
/// Agents have positions, existence, stress. Food tiles have TTL.
/// Scent grid tracks movement trails.
/// </summary>
public class VectorizedState : IDisposable
{
    public int N { get; private set; }
    public Device Device { get; }
    public readonly object LockObj = new();

    // Agent state (CPU arrays — spatial logic is CPU-side)
    public int[] PosX;
    public int[] PosY;
    public float[] Existence;
    public float[] Stress;
    public bool[] Alive;
    public long[] LastAction;
    public int[] LastSignalReceived; // -1 = none (legacy, kept for WebSocket)
    public float[,] SignalMemory; // [agentIdx, signalChannel] — decaying memory per channel
    public int[] TickCount;
    public int[] AttackCount;
    public int[] EatCount;
    public int[] FoodEatCount;
    public int[] BigFoodEatCount;
    public int[] CorpseEatCount;
    public int[] SignalCount;
    public string[] StatusMirror;
    public string[] LastActionNameMirror;
    public DateTime[] BirthTimes;
    public int[] LastReproduceTick;
    public bool[] IsHiding;
    public int[] HideStartTick;
    public int[] FacingDirection;  // 0=up, 1=down, 2=left, 3=right
    public int[] SignalAge;        // ticks since last signal received
    public float[] Thirst;         // 0=dehydrated, 100=fully hydrated

    // Grid state
    public List<FoodTile> FoodTiles;
    public List<CorpseTile> CorpseTiles;
    public float[,] ScentGrid; // [GridWidth, GridHeight]
    public int[,] RiverGrid;   // [GridWidth, GridHeight] — 0=land, 1=shallow, 2=deep
    public float[,] WaterSoundGrid; // [GridWidth, GridHeight] — sound intensity from water

    // GPU tensor for batch inference
    public Tensor StateMatrix;

    // Pre-allocated buffer for state assembly
    private float[] _stateAssemblyBuffer;

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
        SignalMemory = new float[n, ToolDefinitions.SignalValues];
        TickCount = new int[n];
        AttackCount = new int[n];
        EatCount = new int[n];
        FoodEatCount = new int[n];
        BigFoodEatCount = new int[n];
        CorpseEatCount = new int[n];
        SignalCount = new int[n];
        StatusMirror = new string[n];
        LastActionNameMirror = new string[n];
        BirthTimes = new DateTime[n];
        LastReproduceTick = new int[n];
        IsHiding = new bool[n];
        HideStartTick = new int[n];
        FacingDirection = new int[n]; // default 0 = up
        SignalAge = new int[n]; // default 0
        Thirst = new float[n];
        for (int i = 0; i < n; i++) Thirst[i] = 100f;

        FoodTiles = new List<FoodTile>();
        CorpseTiles = new List<CorpseTile>();
        ScentGrid = new float[ToolDefinitions.GridWidth, ToolDefinitions.GridHeight];
        RiverGrid = new int[ToolDefinitions.GridWidth, ToolDefinitions.GridHeight];
        WaterSoundGrid = new float[ToolDefinitions.GridWidth, ToolDefinitions.GridHeight];

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

                    bool hasFood = HasFoodAt(gx, gy, out bool isBigFood);
                    bool hasCorpse = HasCorpseAt(gx, gy);
                    bool hasAgent = HasOtherAgentAt(i, gx, gy);

                    float val = 0f;
                    if (hasAgent && (hasFood || hasCorpse)) val = 0.8f;
                    else if (hasAgent) val = 0.5f;
                    else if (hasCorpse) val = 0.6f;
                    else if (isBigFood) val = 0.4f;
                    else if (hasFood) val = 0.2f;

                    _stateAssemblyBuffer[baseIdx + cellIdx] = val;
                }
            }

            // [25-28] self state
            _stateAssemblyBuffer[baseIdx + 25] = Existence[i] / 100f;
            _stateAssemblyBuffer[baseIdx + 26] = Stress[i] / 5f;
            _stateAssemblyBuffer[baseIdx + 27] = LastAction[i] / 6f;
            _stateAssemblyBuffer[baseIdx + 28] = Math.Min(TickCount[i] / 200f, 1f);

            // [29-32] signal memory (4 channels, each decaying independently)
            _stateAssemblyBuffer[baseIdx + 29] = SignalMemory[i, 0];
            _stateAssemblyBuffer[baseIdx + 30] = SignalMemory[i, 1];
            _stateAssemblyBuffer[baseIdx + 31] = SignalMemory[i, 2];
            _stateAssemblyBuffer[baseIdx + 32] = SignalMemory[i, 3];

            // [33-36] scent gradient
            float scentHere = ScentGrid[cx, cy];
            float sNorth = (cy > 0) ? ScentGrid[cx, cy - 1] - scentHere : 0f;
            float sSouth = (cy < H - 1) ? ScentGrid[cx, cy + 1] - scentHere : 0f;
            float sEast = (cx < W - 1) ? ScentGrid[cx + 1, cy] - scentHere : 0f;
            float sWest = (cx > 0) ? ScentGrid[cx - 1, cy] - scentHere : 0f;

            float maxScent = Math.Max(1f, Math.Max(Math.Abs(sNorth),
                Math.Max(Math.Abs(sSouth), Math.Max(Math.Abs(sEast), Math.Abs(sWest)))));
            _stateAssemblyBuffer[baseIdx + 33] = sNorth / maxScent;
            _stateAssemblyBuffer[baseIdx + 34] = sSouth / maxScent;
            _stateAssemblyBuffer[baseIdx + 35] = sEast / maxScent;
            _stateAssemblyBuffer[baseIdx + 36] = sWest / maxScent;

            // [37-39] local stats
            int foodVisible = 0;
            int agentVisible = 0;
            for (int dy = -R; dy <= R; dy++)
            {
                for (int dx = -R; dx <= R; dx++)
                {
                    int gx = cx + dx;
                    int gy = cy + dy;
                    if (gx < 0 || gx >= W || gy < 0 || gy >= H) continue;
                    if (HasFoodAt(gx, gy, out _) || HasCorpseAt(gx, gy)) foodVisible++;
                    if (dx == 0 && dy == 0) continue;
                    if (HasOtherAgentAt(i, gx, gy)) agentVisible++;
                }
            }
            _stateAssemblyBuffer[baseIdx + 37] = Math.Min(foodVisible / 5f, 1f);
            _stateAssemblyBuffer[baseIdx + 38] = Math.Min(agentVisible / 8f, 1f);
            _stateAssemblyBuffer[baseIdx + 39] = Math.Min(scentHere / 10f, 1f);

            // [40] hide state
            _stateAssemblyBuffer[baseIdx + 40] = IsHiding[i] ? 1f : 0f;

            // [41-42] facing direction (unit vector)
            int fd = FacingDirection[i];
            _stateAssemblyBuffer[baseIdx + 41] = fd == 2 ? -1f : fd == 3 ? 1f : 0f; // facing_x
            _stateAssemblyBuffer[baseIdx + 42] = fd == 0 ? -1f : fd == 1 ? 1f : 0f; // facing_y

            // [43] signal age (normalized, 0 = just received, 1 = old)
            _stateAssemblyBuffer[baseIdx + 43] = Math.Min(SignalAge[i] / 20f, 1f);

            // [44] thirst (normalized 0-1)
            _stateAssemblyBuffer[baseIdx + 44] = Thirst[i] / 100f;

            // [45] water sound intensity at current position (normalized)
            _stateAssemblyBuffer[baseIdx + 45] = Math.Min(WaterSoundGrid[cx, cy] / 10f, 1f);
        }

        StateMatrix.Dispose();
        StateMatrix = tensor(_stateAssemblyBuffer, [N, S], device: Device);
    }

    public bool HasFoodAt(int x, int y, out bool isBig)
    {
        isBig = false;
        for (int i = 0; i < FoodTiles.Count; i++)
        {
            var f = FoodTiles[i];
            int w = f.Width > 0 ? f.Width : 1;
            int h = f.Height > 0 ? f.Height : 1;
            if (x >= f.X && x < f.X + w && y >= f.Y && y < f.Y + h)
            {
                isBig = f.IsBig;
                return true;
            }
        }
        return false;
    }

    public bool IsShallowWater(int x, int y)
    {
        if (x < 0 || x >= ToolDefinitions.GridWidth || y < 0 || y >= ToolDefinitions.GridHeight) return false;
        return RiverGrid[x, y] == 1;
    }

    public bool IsDeepWater(int x, int y)
    {
        if (x < 0 || x >= ToolDefinitions.GridWidth || y < 0 || y >= ToolDefinitions.GridHeight) return false;
        return RiverGrid[x, y] == 2;
    }

    public bool IsAdjacentToWater(int x, int y)
    {
        return IsShallowWater(x - 1, y) || IsShallowWater(x + 1, y) ||
               IsShallowWater(x, y - 1) || IsShallowWater(x, y + 1);
    }

    private bool HasCorpseAt(int x, int y)
    {
        for (int i = 0; i < CorpseTiles.Count; i++)
        {
            if (CorpseTiles[i].X == x && CorpseTiles[i].Y == y)
                return true;
        }
        return false;
    }

    private bool HasOtherAgentAt(int excludeIdx, int x, int y)
    {
        for (int i = 0; i < N; i++)
        {
            if (i == excludeIdx || !Alive[i]) continue;
            if (PosX[i] == x && PosY[i] == y)
            {
                // Hidden agents only visible within DetectionRange (2 cells)
                if (IsHiding[i])
                {
                    int dist = Math.Abs(PosX[excludeIdx] - x) + Math.Abs(PosY[excludeIdx] - y);
                    if (dist > 2) continue; // DetectionRange = 2
                }
                return true;
            }
        }
        return false;
    }

    public float[] GetStateBuffer() => _stateAssemblyBuffer;

    /// <summary>Add a new agent (reproduction). Returns the new agent index.</summary>
    public int AddAgent(int x, int y, float existence)
    {
        int newN = N + 1;
        PosX = Resize(PosX, newN); PosX[newN - 1] = x;
        PosY = Resize(PosY, newN); PosY[newN - 1] = y;
        Existence = Resize(Existence, newN); Existence[newN - 1] = existence;
        Stress = Resize(Stress, newN); Stress[newN - 1] = 0f;
        Alive = Resize(Alive, newN); Alive[newN - 1] = true;
        LastAction = Resize(LastAction, newN); LastAction[newN - 1] = 0;
        LastSignalReceived = Resize(LastSignalReceived, newN); LastSignalReceived[newN - 1] = -1;
        SignalMemory = Resize2D(SignalMemory, newN, ToolDefinitions.SignalValues);
        TickCount = Resize(TickCount, newN);
        AttackCount = Resize(AttackCount, newN);
        EatCount = Resize(EatCount, newN);
        FoodEatCount = Resize(FoodEatCount, newN);
        BigFoodEatCount = Resize(BigFoodEatCount, newN);
        CorpseEatCount = Resize(CorpseEatCount, newN);
        SignalCount = Resize(SignalCount, newN);
        StatusMirror = Resize(StatusMirror, newN); StatusMirror[newN - 1] = "ALIVE";
        LastActionNameMirror = Resize(LastActionNameMirror, newN); LastActionNameMirror[newN - 1] = "";
        BirthTimes = Resize(BirthTimes, newN); BirthTimes[newN - 1] = DateTime.UtcNow;
        LastReproduceTick = Resize(LastReproduceTick, newN); LastReproduceTick[newN - 1] = 0;
        IsHiding = Resize(IsHiding, newN); IsHiding[newN - 1] = false;
        HideStartTick = Resize(HideStartTick, newN); HideStartTick[newN - 1] = 0;
        FacingDirection = Resize(FacingDirection, newN); FacingDirection[newN - 1] = 1; // default down
        SignalAge = Resize(SignalAge, newN); SignalAge[newN - 1] = 0;
        Thirst = Resize(Thirst, newN); Thirst[newN - 1] = 100f;

        // Resize GPU tensor and assembly buffer
        StateMatrix.Dispose();
        StateMatrix = torch.zeros(newN, ToolDefinitions.StateSize, device: Device);
        _stateAssemblyBuffer = new float[newN * ToolDefinitions.StateSize];

        N = newN;
        return newN - 1;
    }

    private static T[] Resize<T>(T[] src, int newSize)
    {
        var dst = new T[newSize];
        Array.Copy(src, dst, Math.Min(src.Length, newSize));
        return dst;
    }

    private static T[,] Resize2D<T>(T[,] src, int dim0, int dim1)
    {
        var dst = new T[dim0, dim1];
        for (int i = 0; i < Math.Min(src.GetLength(0), dim0); i++)
            for (int j = 0; j < Math.Min(src.GetLength(1), dim1); j++)
                dst[i, j] = src[i, j];
        return dst;
    }

    public void Dispose()
    {
        StateMatrix?.Dispose();
    }
}
