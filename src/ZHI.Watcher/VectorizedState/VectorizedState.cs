using TorchSharp;
using ZHI.Shared;
using static TorchSharp.torch;

namespace ZHI.Watcher;

public enum PlantStage : byte
{
    Seed = 0,
    Sprout = 1,
    Adult = 2,
    Decay = 3
}

public struct PlantTile
{
    public int X;
    public int Y;
    public float Energy;
    public byte Stage;   // PlantStage cast
    public int Age;      // ticks since creation
    public float Health; // 0-1, tracks environmental fitness
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
    public float[] Energy;
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
    public float[] BodyWater;
    public bool[] IsEating;
    public float[] BodyTemperature;
    public int[] RespawnCount;
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
    public List<PlantTile> Plants;
    public List<CorpseTile> CorpseTiles;
    public float[,] ScentGrid;
    public float[,] FoodScentGrid;
    public int[,] RiverGrid;
    public float[,] WaterSoundGrid;
    public float[,] TemperatureGrid;
    public float[,] ChemicalField;   // [W, H] — continuous chemical diffusion field
    public byte[,] RiverFlow;
    public int[,] DistanceToRiver;
    public byte[,] HeightMap;        // [W, H] — elevation 0..255
    public float[,] Slope;           // [W, H] — gradient magnitude (height units / cell)
    public byte[,] Aspect;           // [W, H] — slope direction (0-7 = N..NW, 255 = flat)
    public float[,] NutrientGrid;    // [W, H] — soil nutrients (0 to MaxNutrient)
    public float[,] SurfaceWaterGrid; // [W, H] — surface water depth (0 to SurfaceWaterMaxDepth)
    public float[,] GroundwaterGrid;  // [W, H] — groundwater saturation (0 to 1)
    public float[,] Permeability;    // [W, H] — soil infiltration multiplier derived from terrain
    public float[,] Pressure;        // [W, H] — atmospheric pressure (hPa)
    public float[,] WindX;           // [W, H] — wind vector X component
    public float[,] WindY;           // [W, H] — wind vector Y component
    public float[,] Sunlight;        // [W, H] — direct sunlight intensity (0-1)
    public byte[,] Biome;            // [W, H] — biome classification

    // Spatial query grids
    private int[] _agentGrid;
    private float[] _plantGrid; // plant energy per cell, 0 = no plant; Also check _plantStage for Decay
    private byte[] _plantStageGrid; // per-cell plant stage
    public float PlantMaxEnergy = 20f;
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
        Energy = new float[n];
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
        BodyWater = new float[n];
        IsEating = new bool[n];
        BodyTemperature = new float[n];
        RespawnCount = new int[n];
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
            BodyWater[i] = 100f; BodyTemperature[i] = 20f;
        }

        Plants = new List<PlantTile>();
        CorpseTiles = new List<CorpseTile>();
        ScentGrid = new float[W, H];
        FoodScentGrid = new float[W, H];
        RiverGrid = new int[W, H];
        WaterSoundGrid = new float[W, H];
        TemperatureGrid = new float[W, H];
        ChemicalField = new float[W, H];
        RiverFlow = new byte[W, H];
        DistanceToRiver = new int[W, H];
        HeightMap = new byte[W, H];
        Slope = new float[W, H];
        Aspect = new byte[W, H];
        NutrientGrid = new float[W, H];
        SurfaceWaterGrid = new float[W, H];
        GroundwaterGrid = new float[W, H];
        Permeability = new float[W, H];
        Pressure = new float[W, H];
        WindX = new float[W, H];
        WindY = new float[W, H];
        Sunlight = new float[W, H];
        Biome = new byte[W, H];

        int gridSize = W * H;
        _agentGrid = new int[gridSize];
        _plantGrid = new float[gridSize];
        _plantStageGrid = new byte[gridSize];
        _corpseGrid = new bool[gridSize];
        CellOccupancy = new short[gridSize];

        StateMatrix = torch.zeros(n, ToolDefinitions.StateSize, device: device);
        _stateAssemblyBuffer = new float[n * ToolDefinitions.StateSize];

        var localRng = rng ?? new Random();
        for (int i = 0; i < n; i++)
        {
            PosX[i] = localRng.Next(W);
            PosY[i] = localRng.Next(H);
            Energy[i] = 100f;
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
        return _plantGrid[x * H + y] > 0f && _plantStageGrid[x * H + y] != (byte)PlantStage.Seed;
    }

    public float GetPlantEnergyAt(int x, int y)
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        if (x < 0 || x >= W || y < 0 || y >= H) return 0f;
        return _plantGrid[x * H + y];
    }

    public PlantStage GetPlantStageAt(int x, int y)
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        if (x < 0 || x >= W || y < 0 || y >= H) return PlantStage.Seed;
        return (PlantStage)_plantStageGrid[x * H + y];
    }

    public bool IsPlantEdible(int x, int y)
    {
        var stage = GetPlantStageAt(x, y);
        return stage is PlantStage.Sprout or PlantStage.Adult or PlantStage.Decay;
    }

    public float GetPlantEatEfficiency(int x, int y)
    {
        return GetPlantStageAt(x, y) switch
        {
            PlantStage.Sprout => 0.6f,
            PlantStage.Adult => 1.0f,
            PlantStage.Decay => 0.3f,
            _ => 0f
        };
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

    public bool IsAnyWater(int x, int y)
    {
        if (x < 0 || x >= ToolDefinitions.GridWidth || y < 0 || y >= ToolDefinitions.GridHeight) return false;
        return RiverGrid[x, y] > 0
            || (SurfaceWaterGrid != null && SurfaceWaterGrid[x, y] > 0);
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

    /// <summary>
    /// Compute slope (gradient magnitude) and aspect (8-direction) from HeightMap.
    /// Slope units: height difference per cell (0..255 range).
    /// Aspect: 0=N,1=NE,2=E,3=SE,4=S,5=SW,6=W,7=NW,255=flat.
    /// </summary>
    public void ComputeSlopeAndAspect()
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        for (int x = 0; x < W; x++)
        {
            for (int y = 0; y < H; y++)
            {
                float dx = 0, dy = 0;
                if (x > 0 && x < W - 1) dx = (HeightMap[x + 1, y] - HeightMap[x - 1, y]) / 2f;
                else if (x == 0 && W > 1) dx = HeightMap[1, y] - HeightMap[0, y];
                else if (x == W - 1 && W > 1) dx = HeightMap[W - 1, y] - HeightMap[W - 2, y];
                if (y > 0 && y < H - 1) dy = (HeightMap[x, y + 1] - HeightMap[x, y - 1]) / 2f;
                else if (y == 0 && H > 1) dy = HeightMap[x, 1] - HeightMap[x, 0];
                else if (y == H - 1 && H > 1) dy = HeightMap[x, H - 1] - HeightMap[x, H - 2];

                Slope[x, y] = MathF.Sqrt(dx * dx + dy * dy);

                if (Slope[x, y] < 0.5f)
                    Aspect[x, y] = ToolDefinitions.AspectFlat;
                else
                    Aspect[x, y] = QuantizeAspect(dx, dy);
            }
        }
    }

    public void ComputePermeability()
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        // Derive soil permeability from natural terrain properties:
        // - Near rivers: alluvial deposits → high permeability
        // - High elevation: thin/rocky soil → low permeability
        // - Steep slopes: thin soil → low permeability
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                float perm = 1.0f;
                // River proximity bonus: +100% at river, tapering to 0 at distance 8
                int dist = DistanceToRiver[x, y];
                if (dist <= 8)
                    perm += 1.0f * (1f - dist / 8f);
                // Height penalty: high ground loses up to 50% permeability
                perm -= (HeightMap[x, y] / 255f) * 0.5f;
                // Slope penalty: steep ground loses up to 40% permeability
                float slopeNorm = MathF.Min(Slope[x, y] / 30f, 1f);
                perm -= slopeNorm * 0.4f;
                Permeability[x, y] = Math.Clamp(perm, 0.2f, 2.0f);
            }
    }

    /// <summary>
    /// Compute atmospheric pressure from temperature (warm = low pressure, cold = high pressure)
    /// and wind from pressure gradient. Call once per tick after temperature update.
    /// </summary>
    public void ComputePressureAndWind(float tempAvg, float pressureTempFactor, float windStrength)
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        // Pressure: P = 1013 - k * (T - Tavg), warm cells have lower pressure
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
                Pressure[x, y] = 1013f - pressureTempFactor * (TemperatureGrid[x, y] - tempAvg) * 10f;

        // Wind = -∇P * windStrength (flows from high to low pressure)
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                float dpdx = 0, dpdy = 0;
                if (x > 0 && x < W - 1) dpdx = (Pressure[x + 1, y] - Pressure[x - 1, y]) / 2f;
                else if (x == 0 && W > 1) dpdx = Pressure[1, y] - Pressure[0, y];
                else if (x == W - 1 && W > 1) dpdx = Pressure[W - 1, y] - Pressure[W - 2, y];
                if (y > 0 && y < H - 1) dpdy = (Pressure[x, y + 1] - Pressure[x, y - 1]) / 2f;
                else if (y == 0 && H > 1) dpdy = Pressure[x, 1] - Pressure[x, 0];
                else if (y == H - 1 && H > 1) dpdy = Pressure[x, H - 1] - Pressure[x, H - 2];
                WindX[x, y] = -dpdx * windStrength;
                WindY[x, y] = -dpdy * windStrength;
            }
    }

    /// <summary>
    /// Compute sunlight per cell from time-of-day and aspect (south-facing = more sun in northern hemisphere).
    /// </summary>
    public void ComputeSunlight(float gameTimeOfDay, float peakIntensity, float aspectSunMult)
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        // Solar elevation: sin curve peaking at 14:00
        float hourAngle = (gameTimeOfDay - 8f) * MathF.PI / 12f;
        float solarElevation = MathF.Sin(hourAngle); // -1 at night, +1 at noon
        if (solarElevation < 0f) solarElevation = 0f;

        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                float sun = solarElevation * peakIntensity;
                // South-facing slopes get more direct sunlight
                byte asp = Aspect[x, y];
                if (asp != ToolDefinitions.AspectFlat)
                {
                    if (ToolDefinitions.IsSunFacingAspect(asp))
                        sun *= aspectSunMult;
                    else if (ToolDefinitions.IsShadeFacingAspect(asp))
                        sun *= 1f / aspectSunMult;
                }
                // High elevation gets slightly more sun (thinner atmosphere)
                sun *= 1f + (HeightMap[x, y] / 255f) * 0.2f;
                Sunlight[x, y] = sun;
            }
    }

    /// <summary>
    /// Derive biome classification from environment variables.
    /// Called once per generation (static) — biome is a terrain property, not a weather property.
    /// </summary>
    public void ComputeBiomes(float tempAvg, BiomeConfig cfg)
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                // Water cells = water biome
                if (RiverGrid[x, y] > 0 || SurfaceWaterGrid[x, y] > 0.5f)
                {
                    Biome[x, y] = ToolDefinitions.BiomeWater;
                    continue;
                }

                // Riverbank
                if (DistanceToRiver[x, y] > 0 && DistanceToRiver[x, y] <= cfg.RiverBankDistance)
                {
                    Biome[x, y] = ToolDefinitions.BiomeRiverBank;
                    continue;
                }

                // Highland
                if (HeightMap[x, y] >= cfg.HighlandHeightMin)
                {
                    Biome[x, y] = ToolDefinitions.BiomeHighland;
                    continue;
                }

                // Valley
                if (HeightMap[x, y] <= cfg.ValleyHeightMax && DistanceToRiver[x, y] <= 8)
                {
                    Biome[x, y] = ToolDefinitions.BiomeValley;
                    continue;
                }

                float gw = GroundwaterGrid[x, y];
                float nu = NutrientGrid[x, y];
                float temp = TemperatureGrid[x, y];

                // Desert: very dry
                if (gw < cfg.DesertAridityMax)
                {
                    Biome[x, y] = ToolDefinitions.BiomeDesert;
                    continue;
                }

                // Wetland: surface water or saturated ground
                if (SurfaceWaterGrid[x, y] > cfg.WetlandWaterMin || gw > 0.6f)
                {
                    Biome[x, y] = ToolDefinitions.BiomeWetland;
                    continue;
                }

                // Jungle: warm + wet + nutrient-rich
                if (gw >= 0.3f && nu >= cfg.JungleNutrientMin && temp >= cfg.JungleTempMin)
                {
                    Biome[x, y] = ToolDefinitions.BiomeJungle;
                    continue;
                }

                // Grassland: moderate conditions
                if (gw < cfg.GrasslandAridityMax)
                {
                    Biome[x, y] = ToolDefinitions.BiomeGrassland;
                    continue;
                }

                // Default: grassland
                Biome[x, y] = ToolDefinitions.BiomeGrassland;
            }
    }

    private static byte QuantizeAspect(float dx, float dy)
    {
        float angle = MathF.Atan2(dy, dx); // atan2(dy,dx): 0=E, PI/2=S, -PI/2=N, ±PI=W
        // Shift so 0 = N (up, dy<0), clockwise: N,NE,E,SE,S,SW,W,NW
        float shifted = angle + MathF.PI / 2f; // now 0=N
        if (shifted < 0) shifted += 2f * MathF.PI;
        int octant = (int)MathF.Round(shifted / (MathF.PI / 4f)) % 8;
        return (byte)octant;
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

    public void UpdatePlantCell(int x, int y, float energy, byte stage)
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        if (x < 0 || x >= W || y < 0 || y >= H) return;
        int idx = x * H + y;
        _plantGrid[idx] = energy;
        _plantStageGrid[idx] = stage;
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
                   || GetCellOccupancy(PosX[i], PosY[i]) >= 2)
                  && attempts < 50);

        Energy[i] = 100f;
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
        BodyWater[i] = 100f;
        IsEating[i] = false;
        BodyTemperature[i] = 20f;
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
        Energy = Resize(Energy, newN); Energy[newN - 1] = existence;
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
        BodyWater = Resize(BodyWater, newN); BodyWater[newN - 1] = 100f;
        IsEating = Resize(IsEating, newN);
        BodyTemperature = Resize(BodyTemperature, newN); BodyTemperature[newN - 1] = 20f;
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
