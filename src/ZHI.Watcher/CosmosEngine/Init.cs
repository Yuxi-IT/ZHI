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
        GenerateRiver();
        _v.ComputeDistanceToRiver(_config.Temperature.RiverLandInfluence);
        ComputeWaterSound();
        SpawnInitialFood();

        // Initialize nutrient grid
        float initNutrient = _config.Nutrient.InitialNutrient;
        for (int x = 0; x < gw; x++)
            for (int y = 0; y < gh; y++)
                _v.NutrientGrid[x, y] = initNutrient;

        // Initialize agent bodies from random genomes
        for (int i = 0; i < n; i++)
        {
            while (_v.IsDeepWater(_v.PosX[i], _v.PosY[i]))
            {
                _v.PosX[i] = _rng.Next(ToolDefinitions.GridWidth);
                _v.PosY[i] = _rng.Next(ToolDefinitions.GridHeight);
            }
            _v.Thirst[i] = _config.Thirst.Initial;
            _v.Hunger[i] = _config.Hunger.Initial;

            var genome = Genome.Random(_rng, _config.Genome.MutationStd);
            _v.Genomes[i] = genome;
            _v.BodySize[i] = genome.Size;
            _v.BodySpeed[i] = genome.Speed;
            _v.BodyStrength[i] = genome.Strength;
            _v.BodyVision[i] = genome.VisionRange;
            _v.BodyFat[i] = genome.FatStorage;
            _v.BodyColdResist[i] = genome.ColdResistance;
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

        Log($"[Cosmos] Gen {_generation} initialized: {n} agents, {_v.FoodTiles.Count} food");
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
