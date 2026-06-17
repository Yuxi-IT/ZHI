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
    public float[] Hunger;         // 0=starving, 100=fully fed

    // Grid state
    public List<FoodTile> FoodTiles;
    public List<CorpseTile> CorpseTiles;
    public float[,] ScentGrid; // [GridWidth, GridHeight]
    public int[,] RiverGrid;   // [GridWidth, GridHeight] — 0=land, 1=shallow, 2=deep
    public float[,] WaterSoundGrid; // [GridWidth, GridHeight] — sound intensity from water
    public float[,,] SignalField;   // [GridWidth, GridHeight, 4] — spatial signal persistence

    // Spatial query grids (rebuilt each tick, pre-allocated once)
    private int[] _agentGrid;      // [W*H] → agent index or -1
    private byte[] _foodGrid;      // [W*H] → 0=empty, 1=food, 2=bigfood
    private bool[] _corpseGrid;    // [W*H] → has corpse

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
        Hunger = new float[n];
        for (int i = 0; i < n; i++) { Thirst[i] = 100f; Hunger[i] = 100f; }

        FoodTiles = new List<FoodTile>();
        CorpseTiles = new List<CorpseTile>();
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        ScentGrid = new float[W, H];
        RiverGrid = new int[W, H];
        WaterSoundGrid = new float[W, H];
        SignalField = new float[W, H, 4];

        // Spatial query grids (pre-allocated, cleared each tick)
        int gridSize = W * H;
        _agentGrid = new int[gridSize];
        _foodGrid = new byte[gridSize];
        _corpseGrid = new bool[gridSize];

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

    /// <summary>Rebuild spatial query grids. Call once per tick after food/agent movement.</summary>
    public void RebuildSpatialGrids()
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        int gridSize = W * H;

        // Clear grids — no allocation, just zeroing
        for (int i = 0; i < gridSize; i++) _agentGrid[i] = -1;
        Array.Clear(_foodGrid);
        Array.Clear(_corpseGrid);

        // Fill agent grid
        for (int i = 0; i < N; i++)
        {
            if (!Alive[i]) continue;
            int key = PosX[i] * H + PosY[i];
            _agentGrid[key] = i;
        }

        // Fill food grid (handle multi-cell BigFood)
        for (int f = 0; f < FoodTiles.Count; f++)
        {
            var ft = FoodTiles[f];
            int fw = ft.Width > 0 ? ft.Width : 1;
            int fh = ft.Height > 0 ? ft.Height : 1;
            byte val = (byte)(ft.IsBig ? 2 : 1);
            for (int dx = 0; dx < fw; dx++)
                for (int dy = 0; dy < fh; dy++)
                {
                    int x = ft.X + dx, y = ft.Y + dy;
                    if (x >= 0 && x < W && y >= 0 && y < H)
                        _foodGrid[x * H + y] = val;
                }
        }

        // Fill corpse grid
        for (int c = 0; c < CorpseTiles.Count; c++)
        {
            var ct = CorpseTiles[c];
            if (ct.X >= 0 && ct.X < W && ct.Y >= 0 && ct.Y < H)
                _corpseGrid[ct.X * H + ct.Y] = true;
        }
    }

    public void BuildStateMatrix()
    {
        int S = ToolDefinitions.StateSize; // 163
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

            // [0-124] 5×5 grid × 5 channels (one-hot)
            int gridBase = baseIdx;
            for (int dy = -R; dy <= R; dy++)
            {
                for (int dx = -R; dx <= R; dx++)
                {
                    int gx = cx + dx, gy = cy + dy;
                    int cellIdx = ((dy + R) * 5 + (dx + R)) * 5;

                    if (gx < 0 || gx >= W || gy < 0 || gy >= H) continue;

                    bool hasFood = HasFoodAt(gx, gy, out bool isBigFood);
                    bool hasCorpse = HasCorpseAt(gx, gy);
                    bool hasAgent = HasOtherAgentAt(i, gx, gy);
                    bool isSelf = (dx == 0 && dy == 0);

                    _stateAssemblyBuffer[gridBase + cellIdx + 0] = (hasFood && !isBigFood) ? 1f : 0f;
                    _stateAssemblyBuffer[gridBase + cellIdx + 1] = isBigFood ? 1f : 0f;
                    _stateAssemblyBuffer[gridBase + cellIdx + 2] = hasCorpse ? 1f : 0f;
                    _stateAssemblyBuffer[gridBase + cellIdx + 3] = (hasAgent && !isSelf) ? 1f : 0f;
                    _stateAssemblyBuffer[gridBase + cellIdx + 4] = isSelf ? 1f : 0f;
                }
            }

            // [125-128] self state
            _stateAssemblyBuffer[baseIdx + 125] = Existence[i] / 100f;
            _stateAssemblyBuffer[baseIdx + 126] = Stress[i] / 5f;
            _stateAssemblyBuffer[baseIdx + 127] = LastAction[i] / 6f;
            _stateAssemblyBuffer[baseIdx + 128] = Math.Min(TickCount[i] / 200f, 1f);

            // [129-132] signal memory (4 channels, each decaying independently)
            _stateAssemblyBuffer[baseIdx + 129] = SignalMemory[i, 0];
            _stateAssemblyBuffer[baseIdx + 130] = SignalMemory[i, 1];
            _stateAssemblyBuffer[baseIdx + 131] = SignalMemory[i, 2];
            _stateAssemblyBuffer[baseIdx + 132] = SignalMemory[i, 3];

            // [133-136] scent gradient (no dynamic normalization — signals ∈ [0,1])
            float scentHere = ScentGrid[cx, cy];
            _stateAssemblyBuffer[baseIdx + 133] = (cy > 0) ? ScentGrid[cx, cy - 1] - scentHere : 0f;
            _stateAssemblyBuffer[baseIdx + 134] = (cy < H - 1) ? ScentGrid[cx, cy + 1] - scentHere : 0f;
            _stateAssemblyBuffer[baseIdx + 135] = (cx < W - 1) ? ScentGrid[cx + 1, cy] - scentHere : 0f;
            _stateAssemblyBuffer[baseIdx + 136] = (cx > 0) ? ScentGrid[cx - 1, cy] - scentHere : 0f;

            // [137-139] local stats (using spatial grids for O(1) lookup)
            int foodVisible = 0;
            int agentVisible = 0;
            for (int dy = -R; dy <= R; dy++)
            {
                for (int dx = -R; dx <= R; dx++)
                {
                    int gx = cx + dx, gy = cy + dy;
                    if (gx < 0 || gx >= W || gy < 0 || gy >= H) continue;
                    int key = gx * H + gy;
                    if (_foodGrid[key] > 0 || _corpseGrid[key]) foodVisible++;
                    if (dx == 0 && dy == 0) continue;
                    int agentIdx = _agentGrid[key];
                    if (agentIdx >= 0 && agentIdx != i)
                    {
                        if (!IsHiding[agentIdx]) agentVisible++;
                        else
                        {
                            int dist = Math.Abs(dx) + Math.Abs(dy);
                            if (dist <= 2) agentVisible++;
                        }
                    }
                }
            }
            _stateAssemblyBuffer[baseIdx + 137] = Math.Min(foodVisible / 5f, 1f);
            _stateAssemblyBuffer[baseIdx + 138] = Math.Min(agentVisible / 8f, 1f);
            _stateAssemblyBuffer[baseIdx + 139] = Math.Min(scentHere / 10f, 1f);

            // [140] hide state
            _stateAssemblyBuffer[baseIdx + 140] = IsHiding[i] ? 1f : 0f;

            // [141-142] facing direction (unit vector)
            int fd = FacingDirection[i];
            _stateAssemblyBuffer[baseIdx + 141] = fd == 2 ? -1f : fd == 3 ? 1f : 0f;
            _stateAssemblyBuffer[baseIdx + 142] = fd == 0 ? -1f : fd == 1 ? 1f : 0f;

            // [143] signal age (normalized)
            _stateAssemblyBuffer[baseIdx + 143] = Math.Min(SignalAge[i] / 20f, 1f);

            // [144] hunger (normalized 0-1)
            _stateAssemblyBuffer[baseIdx + 144] = Hunger[i] / 100f;

            // [145] thirst (normalized 0-1)
            _stateAssemblyBuffer[baseIdx + 145] = Thirst[i] / 100f;

            // [146] water sound intensity at current position (normalized)
            _stateAssemblyBuffer[baseIdx + 146] = Math.Min(WaterSoundGrid[cx, cy] / 10f, 1f);

            // [147-162] signal field gradient: 4 channels × 4 directions
            for (int ch = 0; ch < 4; ch++)
            {
                float sigHere = SignalField[cx, cy, ch];
                float sigN = (cy > 0) ? SignalField[cx, cy - 1, ch] - sigHere : 0f;
                float sigS = (cy < H - 1) ? SignalField[cx, cy + 1, ch] - sigHere : 0f;
                float sigE = (cx < W - 1) ? SignalField[cx + 1, cy, ch] - sigHere : 0f;
                float sigW = (cx > 0) ? SignalField[cx - 1, cy, ch] - sigHere : 0f;

                // No dynamic normalization — signals ∈ [0,1], gradient ∈ [-1,1]
                _stateAssemblyBuffer[baseIdx + 147 + ch * 4 + 0] = sigN;
                _stateAssemblyBuffer[baseIdx + 147 + ch * 4 + 1] = sigS;
                _stateAssemblyBuffer[baseIdx + 147 + ch * 4 + 2] = sigE;
                _stateAssemblyBuffer[baseIdx + 147 + ch * 4 + 3] = sigW;
            }
        }

        StateMatrix.Dispose();
        StateMatrix = tensor(_stateAssemblyBuffer, [N, S], device: Device);
    }

    public bool HasFoodAt(int x, int y, out bool isBig)
    {
        isBig = false;
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        if (x < 0 || x >= W || y < 0 || y >= H) return false;
        byte val = _foodGrid[x * H + y];
        if (val == 0) return false;
        isBig = val == 2;
        return true;
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
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        if (x < 0 || x >= W || y < 0 || y >= H) return false;
        return _corpseGrid[x * H + y];
    }

    private bool HasOtherAgentAt(int excludeIdx, int x, int y)
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        if (x < 0 || x >= W || y < 0 || y >= H) return false;
        int agentIdx = _agentGrid[x * H + y];
        if (agentIdx < 0 || agentIdx == excludeIdx) return false;
        // Hidden agents only visible within DetectionRange (2 cells)
        if (IsHiding[agentIdx])
        {
            int dist = Math.Abs(PosX[excludeIdx] - x) + Math.Abs(PosY[excludeIdx] - y);
            if (dist > 2) return false;
        }
        return true;
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
        Hunger = Resize(Hunger, newN); Hunger[newN - 1] = 100f;

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
