using TorchSharp;
using ZHI.Shared;
using static TorchSharp.torch;

namespace ZHI.Watcher;

public struct FoodTile
{
    public int X;
    public int Y;
    public float Energy;
}

public struct CorpseTile
{
    public int X;
    public int Y;
    public float Energy;
}

public partial class VectorizedState : IDisposable
{
    public int N { get; private set; }
    public Device Device { get; }
    public readonly object LockObj = new();

    // Agent state (CPU arrays)
    public int[] PosX;
    public int[] PosY;
    public float[] Existence;
    public float[] Stress;
    public bool[] Alive;
    public long[] LastAction;
    public int[] LastSignalReceived; // legacy compat, kept for WebSocket
    public float[] ChemicalMemory;  // [agentIdx] — decaying memory of ambient chemicals
    public int[] TickCount;
    public int[] AttackCount;
    public int[] EatCount;
    public int[] FoodEatCount;
    public int[] CorpseEatCount;
    public int[] EmitCount;        // how many times agent emitted chemical
    public string[] StatusMirror;
    public string[] LastActionNameMirror;
    public DateTime[] BirthTimes;
    public int[] LastReproduceTick;
    public int[] FacingDirection;
    public int[] ChemicalAge;      // ticks since last chemical received
    public float[] Thirst;
    public float[] Hunger;
    public bool[] IsEating;
    public float[] BodyTemperature;
    public int[] RespawnCount;
    public float[] Stamina;
    public int[] TicksSinceLastMove;
    public bool[] IsStationary;

    // Body parameters (derived from Genome, heritable)
    public Genome[] Genomes;
    public float[] BodySize;
    public float[] BodySpeed;
    public float[] BodyStrength;
    public float[] BodyVision;
    public float[] BodyFat;
    public float[] BodyColdResist;

    // Grid state
    public List<FoodTile> FoodTiles;
    public List<CorpseTile> CorpseTiles;
    public float[,] ScentGrid;
    public float[,] FoodScentGrid;
    public int[,] RiverGrid;
    public float[,] WaterSoundGrid;
    public float[,] TemperatureGrid;
    public float[,] ChemicalField;   // [W, H] — continuous chemical diffusion field
    public byte[,] TerrainType;
    public int[,] TerrainTTL;
    public byte[,] RiverFlow;
    public int[,] DistanceToRiver;
    public float[,] HeightMap;       // [W, H] — continuous elevation (-10 to +10)
    public float[,] NutrientGrid;    // [W, H] — soil nutrients (0 to MaxNutrient)
    public float[,] SurfaceWaterGrid; // [W, H] — surface water depth (0 to SurfaceWaterMaxDepth)
    public float[,] GroundwaterGrid;  // [W, H] — groundwater saturation (0 to 1)

    // Spatial query grids
    private int[] _agentGrid;
    private float[] _foodGrid; // plant energy per cell, 0 = no plant
    public float PlantMaxEnergy = 20f; // set by CosmosEngine before BuildStateMatrix
    private bool[] _corpseGrid;
    public short[] CellOccupancy; // live agent count per cell, updated during movement

    // GPU tensor for batch inference
    public Tensor StateMatrix;
    private float[] _stateAssemblyBuffer;

    public VectorizedState(int n, torch.Device device, Random? rng = null)
    {
        N = n;
        Device = device;

        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;

        PosX = new int[n];
        PosY = new int[n];
        Existence = new float[n];
        Stress = new float[n];
        Alive = new bool[n];
        LastAction = new long[n];
        LastSignalReceived = new int[n];
        ChemicalMemory = new float[n];
        TickCount = new int[n];
        AttackCount = new int[n];
        EatCount = new int[n];
        FoodEatCount = new int[n];
        CorpseEatCount = new int[n];
        EmitCount = new int[n];
        StatusMirror = new string[n];
        LastActionNameMirror = new string[n];
        BirthTimes = new DateTime[n];
        LastReproduceTick = new int[n];
        FacingDirection = new int[n];
        ChemicalAge = new int[n];
        Thirst = new float[n];
        Hunger = new float[n];
        IsEating = new bool[n];
        BodyTemperature = new float[n];
        RespawnCount = new int[n];
        Stamina = new float[n];
        TicksSinceLastMove = new int[n];
        IsStationary = new bool[n];

        Genomes = new Genome[n];
        BodySize = new float[n];
        BodySpeed = new float[n];
        BodyStrength = new float[n];
        BodyVision = new float[n];
        BodyFat = new float[n];
        BodyColdResist = new float[n];

        for (int i = 0; i < n; i++)
        {
            Thirst[i] = 100f; Hunger[i] = 100f; BodyTemperature[i] = 20f; Stamina[i] = 100f;
        }

        FoodTiles = new List<FoodTile>();
        CorpseTiles = new List<CorpseTile>();
        ScentGrid = new float[W, H];
        FoodScentGrid = new float[W, H];
        RiverGrid = new int[W, H];
        WaterSoundGrid = new float[W, H];
        TemperatureGrid = new float[W, H];
        ChemicalField = new float[W, H];
        TerrainType = new byte[W, H];
        TerrainTTL = new int[W, H];
        RiverFlow = new byte[W, H];
        DistanceToRiver = new int[W, H];
        HeightMap = new float[W, H];
        NutrientGrid = new float[W, H];
        SurfaceWaterGrid = new float[W, H];
        GroundwaterGrid = new float[W, H];

        int gridSize = W * H;
        _agentGrid = new int[gridSize];
        _foodGrid = new float[gridSize];
        _corpseGrid = new bool[gridSize];
        CellOccupancy = new short[gridSize];

        StateMatrix = torch.zeros(n, ToolDefinitions.StateSize, device: device);
        _stateAssemblyBuffer = new float[n * ToolDefinitions.StateSize];

        var localRng = rng ?? new Random();
        for (int i = 0; i < n; i++)
        {
            PosX[i] = localRng.Next(W);
            PosY[i] = localRng.Next(H);
            Existence[i] = 100f;
            Stress[i] = 0f;
            Alive[i] = true;
            LastAction[i] = 0;
            LastSignalReceived[i] = -1;
            TickCount[i] = 0;
            AttackCount[i] = 0;
            EatCount[i] = 0;
            EmitCount[i] = 0;
            StatusMirror[i] = "idle";
            LastActionNameMirror[i] = "none";
            BirthTimes[i] = DateTime.UtcNow;
        }
    }

    public bool HasFoodAt(int x, int y)
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        if (x < 0 || x >= W || y < 0 || y >= H) return false;
        return _foodGrid[x * H + y] > 0f;
    }

    public float GetPlantEnergyAt(int x, int y)
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        if (x < 0 || x >= W || y < 0 || y >= H) return 0f;
        return _foodGrid[x * H + y];
    }

    public bool IsShallowWater(int x, int y)
    {
        if (x < 0 || x >= ToolDefinitions.GridWidth || y < 0 || y >= ToolDefinitions.GridHeight) return false;
        return RiverGrid[x, y] == 1 || (SurfaceWaterGrid != null && SurfaceWaterGrid[x, y] > 0 && SurfaceWaterGrid[x, y] < 1f);
    }

    public bool IsDeepWater(int x, int y)
    {
        if (x < 0 || x >= ToolDefinitions.GridWidth || y < 0 || y >= ToolDefinitions.GridHeight) return false;
        return RiverGrid[x, y] == 2 || (SurfaceWaterGrid != null && SurfaceWaterGrid[x, y] >= 1f);
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

    public bool IsAnyWater(int x, int y)
    {
        if (x < 0 || x >= ToolDefinitions.GridWidth || y < 0 || y >= ToolDefinitions.GridHeight) return false;
        return RiverGrid[x, y] > 0 || TerrainType[x, y] == ToolDefinitions.TerrainDynamicWater
            || (SurfaceWaterGrid != null && SurfaceWaterGrid[x, y] > 0);
    }

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

    public void ComputeDistanceToRiver(int maxInfluence)
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        int sentinel = maxInfluence + 1;

        var queue = new Queue<(int x, int y)>();
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                if (RiverGrid[x, y] > 0)
                {
                    DistanceToRiver[x, y] = 0;
                    queue.Enqueue((x, y));
                }
                else
                {
                    DistanceToRiver[x, y] = sentinel;
                }
            }

        while (queue.Count > 0)
        {
            var (cx, cy) = queue.Dequeue();
            int nd = DistanceToRiver[cx, cy] + 1;
            if (nd > maxInfluence) continue;

            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = cx + dx, ny = cy + dy;
                    if (nx < 0 || nx >= W || ny < 0 || ny >= H) continue;
                    if (nd < DistanceToRiver[nx, ny])
                    {
                        DistanceToRiver[nx, ny] = nd;
                        queue.Enqueue((nx, ny));
                    }
                }
        }
    }

    public int GetCellOccupancy(int x, int y)
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        if (x < 0 || x >= W || y < 0 || y >= H) return 0;
        return CellOccupancy[x * H + y];
    }

    public void MoveAgentCell(int fromX, int fromY, int toX, int toY)
    {
        int W = ToolDefinitions.GridWidth, H = ToolDefinitions.GridHeight;
        if (fromX >= 0 && fromX < W && fromY >= 0 && fromY < H)
            CellOccupancy[fromX * H + fromY]--;
        if (toX >= 0 && toX < W && toY >= 0 && toY < H)
            CellOccupancy[toX * H + toY]++;
    }

    public void RebuildCellOccupancy()
    {
        int gridSize = ToolDefinitions.GridWidth * ToolDefinitions.GridHeight;
        Array.Clear(CellOccupancy);
        for (int i = 0; i < N; i++)
        {
            if (!Alive[i]) continue;
            int key = PosX[i] * ToolDefinitions.GridHeight + PosY[i];
            CellOccupancy[key]++;
        }
    }

    public bool HasAnyAgentAt(int x, int y)
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        if (x < 0 || x >= W || y < 0 || y >= H) return false;
        return _agentGrid[x * H + y] >= 0;
    }

    public bool HasCorpseAt(int x, int y)
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        if (x < 0 || x >= W || y < 0 || y >= H) return false;
        return _corpseGrid[x * H + y];
    }

    public bool HasOtherAgentAt(int excludeIdx, int x, int y)
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

        int attempts = 0;
        do
        {
            PosX[i] = rng.Next(W);
            PosY[i] = rng.Next(H);
            attempts++;
        } while ((IsDeepWater(PosX[i], PosY[i])
                   || TerrainType[PosX[i], PosY[i]] == ToolDefinitions.TerrainDynamicWater
                   || GetCellOccupancy(PosX[i], PosY[i]) >= 2)
                  && attempts < 50);

        Existence[i] = 100f;
        Stress[i] = 0f;
        Alive[i] = true;
        LastAction[i] = 0;
        LastSignalReceived[i] = -1;
        ChemicalMemory[i] = 0f;
        TickCount[i] = 0;
        AttackCount[i] = 0;
        EatCount[i] = 0;
        FoodEatCount[i] = 0;
        CorpseEatCount[i] = 0;
        EmitCount[i] = 0;
        StatusMirror[i] = "ALIVE";
        LastActionNameMirror[i] = "none";
        BirthTimes[i] = DateTime.UtcNow;
        LastReproduceTick[i] = 0;
        FacingDirection[i] = rng.Next(4);
        ChemicalAge[i] = 0;
        Thirst[i] = 100f;
        Hunger[i] = 100f;
        IsEating[i] = false;
        BodyTemperature[i] = 20f;
        Stamina[i] = 100f;
        TicksSinceLastMove[i] = 0;
        IsStationary[i] = false;
        RespawnCount[i]++;
    }

    public float[] GetStateBuffer() => _stateAssemblyBuffer;

    public int AddAgent(int x, int y, float existence)
    {
        int newN = N + 1;
        PosX = Resize(PosX, newN); PosX[newN - 1] = x;
        PosY = Resize(PosY, newN); PosY[newN - 1] = y;
        Existence = Resize(Existence, newN); Existence[newN - 1] = existence;
        Stress = Resize(Stress, newN);
        Alive = Resize(Alive, newN); Alive[newN - 1] = true;
        LastAction = Resize(LastAction, newN);
        LastSignalReceived = Resize(LastSignalReceived, newN); LastSignalReceived[newN - 1] = -1;
        ChemicalMemory = Resize(ChemicalMemory, newN);
        TickCount = Resize(TickCount, newN);
        AttackCount = Resize(AttackCount, newN);
        EatCount = Resize(EatCount, newN);
        FoodEatCount = Resize(FoodEatCount, newN);
        CorpseEatCount = Resize(CorpseEatCount, newN);
        EmitCount = Resize(EmitCount, newN);
        StatusMirror = Resize(StatusMirror, newN); StatusMirror[newN - 1] = "ALIVE";
        LastActionNameMirror = Resize(LastActionNameMirror, newN); LastActionNameMirror[newN - 1] = "";
        BirthTimes = Resize(BirthTimes, newN); BirthTimes[newN - 1] = DateTime.UtcNow;
        LastReproduceTick = Resize(LastReproduceTick, newN);
        FacingDirection = Resize(FacingDirection, newN); FacingDirection[newN - 1] = 1;
        ChemicalAge = Resize(ChemicalAge, newN);
        Thirst = Resize(Thirst, newN); Thirst[newN - 1] = 100f;
        Hunger = Resize(Hunger, newN); Hunger[newN - 1] = 100f;
        IsEating = Resize(IsEating, newN);
        BodyTemperature = Resize(BodyTemperature, newN); BodyTemperature[newN - 1] = 20f;
        Stamina = Resize(Stamina, newN); Stamina[newN - 1] = 100f;
        TicksSinceLastMove = Resize(TicksSinceLastMove, newN);
        IsStationary = Resize(IsStationary, newN);
        RespawnCount = Resize(RespawnCount, newN);

        Genomes = Resize(Genomes, newN);
        BodySize = Resize(BodySize, newN);
        BodySpeed = Resize(BodySpeed, newN);
        BodyStrength = Resize(BodyStrength, newN);
        BodyVision = Resize(BodyVision, newN);
        BodyFat = Resize(BodyFat, newN);
        BodyColdResist = Resize(BodyColdResist, newN);

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

    public void Dispose()
    {
        StateMatrix?.Dispose();
    }
}
