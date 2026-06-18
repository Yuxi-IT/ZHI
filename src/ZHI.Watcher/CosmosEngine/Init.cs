using TorchSharp;
using ZHI.Core;
using ZHI.Shared;
using static TorchSharp.torch;

namespace ZHI.Watcher;

public partial class CosmosEngine
{
    private void InitializeGeneration(List<byte[]>? loadWeights)
    {
        ToolDefinitions.GridWidth = _config.Grid.Width;
        ToolDefinitions.GridHeight = _config.Grid.Height;

        _genResults.Clear();
        _globalTick = 0;
        _genAttacks = 0;
        _genFoodEaten = 0;
        _genCorpsesEaten = 0;
        _genTotalTicks = 0;
        _genFoodEnergy = 0;
        _genCorpseEnergy = 0;

        int n = _config.Cosmos.AgentCount;
        _v.Dispose();
        _v = new VectorizedState(n, ZHI.Core.Device.TorchDevice);

        _ppoBuffer?.Dispose();
        _ppoBuffer = new PPOBuffer(PpoRolloutSteps, n, ZHI.Core.Device.TorchDevice);

        int gw = ToolDefinitions.GridWidth, gh = ToolDefinitions.GridHeight;
        _scentBuf = new float[gw * gh];
        _foodScentBuf = new float[gw * gh];
        ResizeTickBuffers(n);

        _deathTick = new int[n];
        Array.Fill(_deathTick, -1);
        _lastAttackTick = new int[n];

        Array.Clear(_v.ScentGrid);
        Array.Clear(_v.RiverGrid);
        Array.Clear(_v.WaterSoundGrid);
        GenerateHeightMap();
        _v.ComputeSlopeAndAspect();
        _v.ComputePermeability();
        GenerateRiver();
        _v.ComputeDistanceToRiver(_config.Temperature.RiverLandInfluence);
        ComputeWaterSound();
        SpawnInitialFood();

        // Initialize nutrient grid — riverbank gradient (river=0, bank=peak, inland=baseline)
        float baseNutrient = _config.Nutrient.InitialNutrient;
        float bankBoost = _config.Nutrient.RiverBankNutrientBoost;
        int bankDist = _config.Nutrient.RiverBankDistance;
        for (int x = 0; x < gw; x++)
            for (int y = 0; y < gh; y++)
            {
                int dist = _v.DistanceToRiver[x, y];
                if (dist == 0)
                    _v.NutrientGrid[x, y] = 0f;
                else if (dist <= bankDist)
                    _v.NutrientGrid[x, y] = baseNutrient + bankBoost * (1f - (dist - 1f) / bankDist);
                else
                    _v.NutrientGrid[x, y] = baseNutrient;
            }

        // Initialize surface water from river
        for (int x = 0; x < gw; x++)
            for (int y = 0; y < gh; y++)
                _v.SurfaceWaterGrid[x, y] = _v.RiverGrid[x, y] switch
                {
                    2 => 2f,
                    1 => 1f,
                    _ => 0f
                };

        // Initialize groundwater near rivers (taper by distance)
        for (int x = 0; x < gw; x++)
            for (int y = 0; y < gh; y++)
            {
                int dist = _v.DistanceToRiver[x, y];
                if (dist == 0)
                    _v.GroundwaterGrid[x, y] = _config.WaterCycle.MaxGroundwater;
                else if (dist <= 5)
                    _v.GroundwaterGrid[x, y] = _config.WaterCycle.MaxGroundwater * (1f - dist / 6f);
                else
                    _v.GroundwaterGrid[x, y] = 0.1f;
            }

        // Initialize temperature grid to correct ambient before biome derivation
        ApplyWorldTemperature(n);

        // Rapid-converge land cells to avoid cold-start
        float heightLapseRate = _config.Temperature.HeightLapseRate;
        for (int x = 0; x < gw; x++)
            for (int y = 0; y < gh; y++)
            {
                if (_v.RiverGrid[x, y] > 0 || _v.SurfaceWaterGrid[x, y] > 0.5f) continue;
                float targetTemp = _temperature - (_v.HeightMap[x, y] - 128) * heightLapseRate;
                _v.TemperatureGrid[x, y] = targetTemp;
            }

        // Compute biome classification from terrain + initial conditions
        _v.ComputeBiomes(_temperature, _config.Biome);

        // Schedule first rain
        _nextRainTick = _rng.Next(_config.WaterCycle.RainIntervalMin, _config.WaterCycle.RainIntervalMax);
        _humidity = 0.5f;

        // Initialize agent bodies from random genomes
        for (int i = 0; i < n; i++)
        {
            while (_v.IsDeepWater(_v.PosX[i], _v.PosY[i])
                   || _v.IsShallowWater(_v.PosX[i], _v.PosY[i]))
            {
                _v.PosX[i] = _rng.Next(ToolDefinitions.GridWidth);
                _v.PosY[i] = _rng.Next(ToolDefinitions.GridHeight);
            }
            _v.BodyWater[i] = _config.Metabolism.WaterInitial;

            var genome = Genome.Random(_rng, _config.Genome.MutationStd);
            _v.Genomes[i] = genome;
            _v.BodySize[i] = genome.Size;
            _v.BodySpeed[i] = genome.Speed;
            _v.BodyStrength[i] = genome.Strength;
            _v.BodyVision[i] = genome.VisionRange;
            _v.BodyFat[i] = genome.FatStorage;
            _v.BodyColdResist[i] = genome.ColdResistance;
            _v.BodyHeatResist[i] = genome.HeatResistance;
        }

        if (loadWeights != null && loadWeights.Count > 0)
        {
            try
            {
                _gruBrain.LoadWeights(loadWeights[0]);
            }
            catch (Exception ex)
            {
                Log($"[Cosmos] Failed to load weights (dimension mismatch?): {ex.Message}. Starting fresh.");
                _gruBrain.Dispose();
                _gruBrain = new GRUBrain(_config.Network.LearningRate, _config.Network.Gamma);
            }
        }
        else
        {
            _gruBrain.Dispose();
            _gruBrain = new GRUBrain(_config.Network.LearningRate, _config.Network.Gamma);
        }

        _gruHidden?.Dispose();
        _gruHidden = torch.zeros(1, n, _gruBrain.HiddenSize, device: _v.Device);

        _agentWeights.Clear();
        for (int i = 0; i < n; i++)
        {
            if (loadWeights != null && i < loadWeights.Count)
                _agentWeights.Add(loadWeights[i]);
            else
                _agentWeights.Add(_gruBrain.SaveWeights());
        }

        // Build initial state matrix so first Tick sees the world (visibility-aware)
        _v.PlantMaxEnergy = _config.Plant.MaxPlantEnergy;
        _v.RebuildSpatialGrids();
        _v.ComputeVisibilityBlock();
        _v.BuildStateMatrix();

        Log($"[Cosmos] Gen {_generation} initialized: {n} agents, {_v.Plants.Count} food");
    }

    private void ResizeTickBuffers(int n)
    {
        if (_rewardBuf.Length < n) _rewardBuf = new float[n];
        if (_donesBuf.Length < n) _donesBuf = new float[n];
        if (_actionsBuf.Length < n) _actionsBuf = new long[n];
        if (_signalBuf.Length < n) _signalBuf = new float[n];
        if (_logProbsBuf.Length < n) _logProbsBuf = new float[n];
        if (_valuesBuf.Length < n) _valuesBuf = new float[n];
        if (_aliveMaskBuf.Length < n) _aliveMaskBuf = new float[n];
        if (_intrinsicBuf.Length < n) _intrinsicBuf = new float[n];
    }
}
