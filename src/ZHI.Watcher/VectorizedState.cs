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
    public float Energy;
    public bool IsBig;
}

public struct CorpseTile
{
    public int X;
    public int Y;
    public float Energy;
}

/// <summary>
/// Spatial grid world state for 128×128 ecosystem.
/// Agents have positions, existence, stress. Food tiles have TTL.
/// Scent grid tracks movement trails.
/// </summary>
public class VectorizedState : IDisposable
{
    // Forward-facing visibility mask (facing up, 7×7). 1=visible, 0=hidden.
    // Agent at center (row 3). Can see forward cone, nothing behind.
    private static readonly bool[,] BaseVisionMask = {
        { true,  true,  true,  true,  true,  true,  true  },  // dy=-3
        { false, true,  true,  true,  true,  true,  false },  // dy=-2
        { false, false, true,  true,  true,  false, false },  // dy=-1
        { false, false, false, true,  false, false, false },  // dy=0 (self)
        { false, false, false, false, false, false, false },  // dy=+1
        { false, false, false, false, false, false, false },  // dy=+2
        { false, false, false, false, false, false, false },  // dy=+3
    };
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
    public int[] FacingDirection;  // 0=up, 1=down, 2=left, 3=right
    public int[] SignalAge;        // ticks since last signal received
    public float[] Thirst;         // 0=dehydrated, 100=fully hydrated
    public float[] Hunger;         // 0=starving, 100=fully fed
    public bool[] IsEating;        // whether agent is in eating toggle state
    public float[] BodyTemperature; // agent's own body temperature (tracks local env with inertia)
    public int[] RespawnCount;     // how many times this agent slot has respawned
    public float[] Stamina;        // 0-100, consumed by high-effort actions
    public int[] TicksSinceLastMove; // counter for stationary detection
    public bool[] IsStationary;    // true when TicksSinceLastMove >= threshold
    public int[] PushCount;        // how many times agent pushed an entity
    public int[] TerraformCount;   // how many times agent changed terrain

    // Grid state
    public List<FoodTile> FoodTiles;
    public List<CorpseTile> CorpseTiles;
    public float[,] ScentGrid;     // [GridWidth, GridHeight] — agent movement trails
    public float[,] FoodScentGrid; // [GridWidth, GridHeight] — food/corpse scent
    public int[,] RiverGrid;       // [GridWidth, GridHeight] — 0=land, 1=shallow, 2=deep
    public float[,] WaterSoundGrid; // [GridWidth, GridHeight] — sound intensity from water
    public float[,] TemperatureGrid; // [GridWidth, GridHeight] — local temperature with body heat
    public float[,,] SignalField;   // [GridWidth, GridHeight, 4] — spatial signal persistence
    public byte[,] TerrainType;     // [GridWidth, GridHeight] — 0=Flat, 1=Pit, 2=Mound, 3=DynamicWater
    public int[,] TerrainTTL;       // [GridWidth, GridHeight] — remaining lifespan (0=permanent/inactive)

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
        FacingDirection = new int[n]; // default 0 = up
        SignalAge = new int[n]; // default 0
        Thirst = new float[n];
        Hunger = new float[n];
        IsEating = new bool[n];
        BodyTemperature = new float[n];
        RespawnCount = new int[n];
        Stamina = new float[n];
        TicksSinceLastMove = new int[n];
        IsStationary = new bool[n];
        PushCount = new int[n];
        TerraformCount = new int[n];
        for (int i = 0; i < n; i++) { Thirst[i] = 100f; Hunger[i] = 100f; BodyTemperature[i] = 20f; Stamina[i] = 100f; }

        FoodTiles = new List<FoodTile>();
        CorpseTiles = new List<CorpseTile>();
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        ScentGrid = new float[W, H];
        FoodScentGrid = new float[W, H];
        RiverGrid = new int[W, H];
        WaterSoundGrid = new float[W, H];
        TemperatureGrid = new float[W, H];
        SignalField = new float[W, H, 4];
        TerrainType = new byte[W, H];
        TerrainTTL = new int[W, H];

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
        int S = ToolDefinitions.StateSize; // 334
        int R = ToolDefinitions.VisionRadius;
        int D = R * 2 + 1; // 7
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        const int GridCh = 6; // food, bigfood, corpse, agent, self, terrain

        Array.Clear(_stateAssemblyBuffer);

        for (int i = 0; i < N; i++)
        {
            if (!Alive[i]) continue;

            int baseIdx = i * S;
            int cx = PosX[i];
            int cy = PosY[i];

            // [0-293] 7×7 grid × 6 channels: food, bigfood, corpse, agent, self, terrain
            int gridBase = baseIdx;
            int fd = FacingDirection[i];
            for (int dy = -R; dy <= R; dy++)
            {
                for (int dx = -R; dx <= R; dx++)
                {
                    int cellIdx = ((dy + R) * D + (dx + R)) * GridCh;

                    // Directional visibility: rotate offset to base (facing-up) frame
                    int rdx, rdy;
                    switch (fd)
                    {
                        case 0: rdx = dx; rdy = dy; break;       // up
                        case 1: rdx = -dx; rdy = -dy; break;     // down
                        case 2: rdx = -dy; rdy = dx; break;      // left
                        default: rdx = dy; rdy = -dx; break;     // right
                    }
                    int maskCol = rdx + R, maskRow = rdy + R;
                    if (maskCol < 0 || maskCol >= D || maskRow < 0 || maskRow >= D
                        || !BaseVisionMask[maskRow, maskCol])
                        continue;

                    int gx = cx + dx, gy = cy + dy;
                    if (gx < 0 || gx >= W || gy < 0 || gy >= H) continue;

                    bool hasFood = HasFoodAt(gx, gy, out bool isBigFood);
                    bool hasCorpse = HasCorpseAt(gx, gy);
                    bool hasAgent = HasOtherAgentAt(i, gx, gy);
                    bool isSelf = (dx == 0 && dy == 0);
                    byte terrain = TerrainType[gx, gy];
                    // Flat=0, Pit=0.33, Mound=0.66, Water=1.0
                    float terrainNorm = terrain == 1 ? 0.33f : terrain == 2 ? 0.66f : terrain >= 3 || RiverGrid[gx, gy] > 0 ? 1f : 0f;

                    _stateAssemblyBuffer[gridBase + cellIdx + 0] = (hasFood && !isBigFood) ? 1f : 0f;
                    _stateAssemblyBuffer[gridBase + cellIdx + 1] = isBigFood ? 1f : 0f;
                    _stateAssemblyBuffer[gridBase + cellIdx + 2] = hasCorpse ? 1f : 0f;
                    _stateAssemblyBuffer[gridBase + cellIdx + 3] = (hasAgent && !isSelf) ? 1f : 0f;
                    _stateAssemblyBuffer[gridBase + cellIdx + 4] = isSelf ? 1f : 0f;
                    _stateAssemblyBuffer[gridBase + cellIdx + 5] = terrainNorm;
                }
            }

            // [294-298] self state (5 dims: HP, Stress, LastAction, Age, Stamina)
            _stateAssemblyBuffer[baseIdx + 294] = Existence[i] / 100f;
            _stateAssemblyBuffer[baseIdx + 295] = Stress[i] / 5f;
            _stateAssemblyBuffer[baseIdx + 296] = LastAction[i] / 9f; // now 10 actions
            _stateAssemblyBuffer[baseIdx + 297] = Math.Min(TickCount[i] / 200f, 1f);
            _stateAssemblyBuffer[baseIdx + 298] = Stamina[i] / 100f;

            // [299-302] signal memory (4 channels)
            _stateAssemblyBuffer[baseIdx + 299] = SignalMemory[i, 0];
            _stateAssemblyBuffer[baseIdx + 300] = SignalMemory[i, 1];
            _stateAssemblyBuffer[baseIdx + 301] = SignalMemory[i, 2];
            _stateAssemblyBuffer[baseIdx + 302] = SignalMemory[i, 3];

            // [303-306] scent gradient
            float scentHere = ScentGrid[cx, cy];
            _stateAssemblyBuffer[baseIdx + 303] = (cy > 0) ? ScentGrid[cx, cy - 1] - scentHere : 0f;
            _stateAssemblyBuffer[baseIdx + 304] = (cy < H - 1) ? ScentGrid[cx, cy + 1] - scentHere : 0f;
            _stateAssemblyBuffer[baseIdx + 305] = (cx < W - 1) ? ScentGrid[cx + 1, cy] - scentHere : 0f;
            _stateAssemblyBuffer[baseIdx + 306] = (cx > 0) ? ScentGrid[cx - 1, cy] - scentHere : 0f;

            // [307-309] local stats (using same directional visibility)
            int foodVisible = 0;
            int agentVisible = 0;
            for (int dy = -R; dy <= R; dy++)
            {
                for (int dx = -R; dx <= R; dx++)
                {
                    int rdx2, rdy2;
                    switch (fd)
                    {
                        case 0: rdx2 = dx; rdy2 = dy; break;
                        case 1: rdx2 = -dx; rdy2 = -dy; break;
                        case 2: rdx2 = -dy; rdy2 = dx; break;
                        default: rdx2 = dy; rdy2 = -dx; break;
                    }
                    int mc = rdx2 + R, mr = rdy2 + R;
                    if (mc < 0 || mc >= D || mr < 0 || mr >= D
                        || !BaseVisionMask[mr, mc])
                        continue;

                    int gx = cx + dx, gy = cy + dy;
                    if (gx < 0 || gx >= W || gy < 0 || gy >= H) continue;
                    int key = gx * H + gy;
                    if (_foodGrid[key] > 0 || _corpseGrid[key]) foodVisible++;
                    if (dx == 0 && dy == 0) continue;
                    int agentIdx = _agentGrid[key];
                    if (agentIdx >= 0 && agentIdx != i) agentVisible++;
                }
            }
            _stateAssemblyBuffer[baseIdx + 307] = Math.Min(foodVisible / 5f, 1f);
            _stateAssemblyBuffer[baseIdx + 308] = Math.Min(agentVisible / 8f, 1f);
            _stateAssemblyBuffer[baseIdx + 309] = Math.Min(scentHere / 10f, 1f);

            // [310-311] facing direction (unit vector)
            _stateAssemblyBuffer[baseIdx + 310] = fd == 2 ? -1f : fd == 3 ? 1f : 0f;
            _stateAssemblyBuffer[baseIdx + 311] = fd == 0 ? -1f : fd == 1 ? 1f : 0f;

            // [312] signal age (normalized)
            _stateAssemblyBuffer[baseIdx + 312] = Math.Min(SignalAge[i] / 20f, 1f);

            // [313] hunger (normalized 0-1)
            _stateAssemblyBuffer[baseIdx + 313] = Hunger[i] / 100f;

            // [314] thirst (normalized 0-1)
            _stateAssemblyBuffer[baseIdx + 314] = Thirst[i] / 100f;

            // [315] water sound intensity at current position (normalized)
            _stateAssemblyBuffer[baseIdx + 315] = Math.Min(WaterSoundGrid[cx, cy] / 10f, 1f);

            // [316] is_eating
            _stateAssemblyBuffer[baseIdx + 316] = IsEating[i] ? 1f : 0f;

            // [317] is_stationary
            _stateAssemblyBuffer[baseIdx + 317] = IsStationary[i] ? 1f : 0f;

            // [318-333] signal field gradient: 4 channels × 4 directions
            for (int ch = 0; ch < 4; ch++)
            {
                float sigHere = SignalField[cx, cy, ch];
                float sigN = (cy > 0) ? SignalField[cx, cy - 1, ch] - sigHere : 0f;
                float sigS = (cy < H - 1) ? SignalField[cx, cy + 1, ch] - sigHere : 0f;
                float sigE = (cx < W - 1) ? SignalField[cx + 1, cy, ch] - sigHere : 0f;
                float sigW = (cx > 0) ? SignalField[cx - 1, cy, ch] - sigHere : 0f;

                _stateAssemblyBuffer[baseIdx + 318 + ch * 4 + 0] = sigN;
                _stateAssemblyBuffer[baseIdx + 318 + ch * 4 + 1] = sigS;
                _stateAssemblyBuffer[baseIdx + 318 + ch * 4 + 2] = sigE;
                _stateAssemblyBuffer[baseIdx + 318 + ch * 4 + 3] = sigW;
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
        return IsAnyWater(x - 1, y) || IsAnyWater(x + 1, y) ||
               IsAnyWater(x, y - 1) || IsAnyWater(x, y + 1);
    }

    public byte GetTerrainAt(int x, int y)
    {
        if (x < 0 || x >= ToolDefinitions.GridWidth || y < 0 || y >= ToolDefinitions.GridHeight) return 0;
        return TerrainType[x, y];
    }

    public bool IsMoundAt(int x, int y)
    {
        return GetTerrainAt(x, y) == ToolDefinitions.TerrainMound;
    }

    /// <summary>True if cell is any kind of water (river shallow/deep or dynamic floodwater).</summary>
    public bool IsAnyWater(int x, int y)
    {
        if (x < 0 || x >= ToolDefinitions.GridWidth || y < 0 || y >= ToolDefinitions.GridHeight) return false;
        return RiverGrid[x, y] > 0 || TerrainType[x, y] == ToolDefinitions.TerrainDynamicWater;
    }

    /// <summary>Count adjacent (Moore 8) cells of a given terrain type, excluding self.</summary>
    public int CountAdjacentTerrain(int x, int y, byte terrainType)
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        int count = 0;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && nx < W && ny >= 0 && ny < H && TerrainType[nx, ny] == terrainType)
                    count++;
            }
        }
        return count;
    }

    public bool HasAnyAgentAt(int x, int y)
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        if (x < 0 || x >= W || y < 0 || y >= H) return false;
        return _agentGrid[x * H + y] >= 0;
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
        return agentIdx >= 0 && agentIdx != excludeIdx;
    }

    public void RespawnAgent(int i, Random rng)
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;

        // Random position avoiding deep water
        int attempts = 0;
        do
        {
            PosX[i] = rng.Next(W);
            PosY[i] = rng.Next(H);
            attempts++;
        } while ((IsDeepWater(PosX[i], PosY[i]) || TerrainType[PosX[i], PosY[i]] == ToolDefinitions.TerrainDynamicWater) && attempts < 50);

        Existence[i] = 100f;
        Stress[i] = 0f;
        Alive[i] = true;
        LastAction[i] = 0;
        LastSignalReceived[i] = -1;
        for (int ch = 0; ch < ToolDefinitions.SignalValues; ch++)
            SignalMemory[i, ch] = 0f;
        TickCount[i] = 0;
        AttackCount[i] = 0;
        EatCount[i] = 0;
        FoodEatCount[i] = 0;
        BigFoodEatCount[i] = 0;
        CorpseEatCount[i] = 0;
        SignalCount[i] = 0;
        StatusMirror[i] = "ALIVE";
        LastActionNameMirror[i] = "none";
        BirthTimes[i] = DateTime.UtcNow;
        LastReproduceTick[i] = 0;
        FacingDirection[i] = rng.Next(4);
        SignalAge[i] = 0;
        Thirst[i] = 100f;
        Hunger[i] = 100f;
        IsEating[i] = false;
        BodyTemperature[i] = 20f; // reset to ambient
        Stamina[i] = 100f;
        TicksSinceLastMove[i] = 0;
        IsStationary[i] = false;
        PushCount[i] = 0;
        TerraformCount[i] = 0;
        RespawnCount[i]++;
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
        FacingDirection = Resize(FacingDirection, newN); FacingDirection[newN - 1] = 1; // default down
        SignalAge = Resize(SignalAge, newN); SignalAge[newN - 1] = 0;
        Thirst = Resize(Thirst, newN); Thirst[newN - 1] = 100f;
        Hunger = Resize(Hunger, newN); Hunger[newN - 1] = 100f;
        IsEating = Resize(IsEating, newN);
        BodyTemperature = Resize(BodyTemperature, newN); BodyTemperature[newN - 1] = 20f;
        Stamina = Resize(Stamina, newN); Stamina[newN - 1] = 100f;
        TicksSinceLastMove = Resize(TicksSinceLastMove, newN);
        IsStationary = Resize(IsStationary, newN);
        PushCount = Resize(PushCount, newN);
        TerraformCount = Resize(TerraformCount, newN);
        RespawnCount = Resize(RespawnCount, newN);

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
