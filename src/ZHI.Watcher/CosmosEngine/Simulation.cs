using ZHI.Shared;

namespace ZHI.Watcher;

public partial class CosmosEngine
{
    private void ApplyWorldTemperature(int n)
    {
        _gameTimeOfDay = ((_globalTick + 1200) % 3600) / 150f;
        float hourAngle = (_gameTimeOfDay - 8f) * MathF.PI / 12f;
        _temperature = 20f + 15f * _seasonTemperatureModifier * MathF.Sin(hourAngle);

        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        float ambientTarget = _temperature;
        float landRate = _config.Temperature.LandLerpRate;
        float waterRate = landRate / _config.Temperature.WaterHeatCapacity;
        float diffRate = _config.Temperature.ThermalDiffusionRate;
        int influence = _config.Temperature.RiverLandInfluence;
        float deepOffset = _config.Temperature.DeepWaterExtraCold;

        // Pre-compute per-cell lerp rate based on distance to nearest water
        var lerpRates = new float[W, H];
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                int dist = _v.DistanceToRiver[x, y];
                if (dist == 0)
                {
                    // Water cell: high heat capacity → slow temp change
                    lerpRates[x, y] = waterRate;
                }
                else if (dist <= influence)
                {
                    // Land near river: interpolated between water and land rates
                    float t = (float)dist / influence;
                    lerpRates[x, y] = waterRate + (landRate - waterRate) * t;
                }
                else
                {
                    // Inland: fast temp change
                    lerpRates[x, y] = landRate;
                }
            }

        // Pass 1: ambient convergence + thermal diffusion (write to new grid)
        var newGrid = new float[W, H];
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                float oldTemp = _v.TemperatureGrid[x, y];
                float target = ambientTarget;

                // Deep water is slightly colder than surface water
                if (_v.DistanceToRiver[x, y] == 0 && _v.RiverGrid[x, y] == 2)
                    target -= deepOffset;

                // Ambient convergence
                float newTemp = oldTemp + (target - oldTemp) * lerpRates[x, y];

                // Thermal diffusion: blend toward average of 4 cardinal neighbors
                float neighborSum = 0f;
                int nc = 0;
                if (x > 0) { neighborSum += _v.TemperatureGrid[x - 1, y]; nc++; }
                if (x < W - 1) { neighborSum += _v.TemperatureGrid[x + 1, y]; nc++; }
                if (y > 0) { neighborSum += _v.TemperatureGrid[x, y - 1]; nc++; }
                if (y < H - 1) { neighborSum += _v.TemperatureGrid[x, y + 1]; nc++; }
                if (nc > 0)
                {
                    float neighborAvg = neighborSum / nc;
                    newTemp += (neighborAvg - newTemp) * diffRate;
                }

                newGrid[x, y] = newTemp;
            }

        // Pass 2: additive contributions — agent body heat
        float bodyHeat = _config.Temperature.AgentBodyHeat;
        float initialHP = _config.Existence.Initial;
        for (int i = 0; i < n; i++)
        {
            if (!_v.Alive[i]) continue;
            int ax = _v.PosX[i], ay = _v.PosY[i];
            float hpRatio = MathF.Max(0f, _v.Existence[i] / initialHP);
            float agentHeat = bodyHeat * hpRatio;

            float selfBonus = _v.IsStationary[i] ? _config.Stamina.StationarySelfHeat : 0f;
            float neighborBonus = _v.IsStationary[i] ? _config.Stamina.StationaryNeighborHeat : 0f;

            newGrid[ax, ay] += agentHeat + selfBonus;
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = ax + dx, ny = ay + dy;
                    if (nx >= 0 && nx < W && ny >= 0 && ny < H)
                        newGrid[nx, ny] += agentHeat * 0.5f + neighborBonus;
                }
        }

        // Pass 3: daytime mound shade
        bool isDaytime = _gameTimeOfDay >= 6f && _gameTimeOfDay < 20f;
        if (isDaytime)
        {
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                    if (_v.TerrainType[x, y] == ToolDefinitions.TerrainMound)
                        newGrid[x, y] -= 3f;
        }

        // Swap grids
        _v.TemperatureGrid = newGrid;
    }

    private void ApplyAgentPhysiology(int n)
    {
        for (int i = 0; i < n; i++)
        {
            if (!_v.Alive[i]) continue;
            _v.Existence[i] -= _v.Stress[i] * _config.Combat.StressDamage;
        }

        for (int i = 0; i < n; i++)
        {
            if (!_v.Alive[i]) continue;
            _v.Stress[i] = MathF.Max(0f, _v.Stress[i] - _config.Combat.StressDecay);
        }

        for (int i = 0; i < n; i++)
        {
            if (!_v.Alive[i]) continue;
            float decay = _config.Existence.DecayPerTick;

            int ax = _v.PosX[i], ay = _v.PosY[i];
            lock (_v.LockObj)
            {
                foreach (var ct in _v.CorpseTiles)
                {
                    int dist = Math.Max(Math.Abs(ct.X - ax), Math.Abs(ct.Y - ay));
                    if (dist <= 2)
                    {
                        float pollution = (3f - dist) * 0.02f;
                        decay += pollution;
                    }
                }
            }

            int age = _v.TickCount[i];
            if (age >= _config.AgeDeath.MaxAge)
            {
                _v.Existence[i] = 0f;
                continue;
            }
            else if (age >= _config.AgeDeath.Stage3Age)
                decay += _config.AgeDeath.Stage3Decay;
            else if (age >= _config.AgeDeath.Stage2Age)
                decay += _config.AgeDeath.Stage2Decay;
            else if (age >= _config.AgeDeath.Stage1Age)
                decay += _config.AgeDeath.Stage1Decay;

            float hungerRatio = MathF.Max(0f, 1f - (_v.Hunger[i] / _config.Hunger.PenaltyStart));
            decay += hungerRatio * _config.Hunger.MaxPenalty;

            float thirstRatio = MathF.Max(0f, 1f - (_v.Thirst[i] / _config.Thirst.PenaltyStart));
            decay += thirstRatio * _config.Thirst.MaxPenalty;

            _v.Existence[i] -= decay;

            if (_v.Hunger[i] > 80f && _v.Thirst[i] > 80f)
                _v.Existence[i] = MathF.Min(_v.Existence[i] + 0.2f, _config.Existence.Initial);
        }

        const float BodyTempLerpRate = 0.05f;
        for (int i = 0; i < n; i++)
        {
            if (!_v.Alive[i]) continue;
            float lerpRate = BodyTempLerpRate;
            // Water accelerates body temp convergence (2x in shallow, 2x in deep)
            int rx = _v.PosX[i], ry = _v.PosY[i];
            if (_v.RiverGrid[rx, ry] > 0 || _v.TerrainType[rx, ry] == ToolDefinitions.TerrainDynamicWater)
                lerpRate *= _config.Temperature.WaterCoolingMult;
            float localTemp = _v.TemperatureGrid[rx, ry];
            _v.BodyTemperature[i] += (localTemp - _v.BodyTemperature[i]) * lerpRate;
            // Hard floor
            _v.BodyTemperature[i] = MathF.Max(_v.BodyTemperature[i], _config.Temperature.MinBodyTemp);
        }

        for (int i = 0; i < n; i++)
        {
            if (!_v.Alive[i]) continue;
            float bodyTemp = _v.BodyTemperature[i];

            // Hypothermia damage: body temp below 33°C → linear HP decay
            float hypoThreshold = _config.Temperature.HypothermiaThreshold;
            if (bodyTemp < hypoThreshold)
            {
                float hypoRatio = (hypoThreshold - bodyTemp)
                    / (hypoThreshold - _config.Temperature.MinBodyTemp);
                float hypoDecay = hypoRatio * _config.Temperature.HypothermiaMaxDamage;
                _v.Existence[i] -= hypoDecay;
            }

            if (bodyTemp < _config.Temperature.ColdThreshold)
            {
                float coldRatio = 1f - (bodyTemp - _config.Temperature.MinTemp)
                    / (_config.Temperature.ColdThreshold - _config.Temperature.MinTemp);
                float coldDecay = coldRatio * _config.Temperature.MaxColdDecay;
                if (_v.GetTerrainAt(_v.PosX[i], _v.PosY[i]) == ToolDefinitions.TerrainPit
                    && _v.CountAdjacentTerrain(_v.PosX[i], _v.PosY[i], ToolDefinitions.TerrainPit) == 0)
                    coldDecay *= 0.7f;
                _v.Existence[i] -= coldDecay;
            }

            if (bodyTemp > _config.Temperature.HotThreshold)
            {
                float hotRatio = (bodyTemp - _config.Temperature.HotThreshold)
                    / (_config.Temperature.MaxTemp - _config.Temperature.HotThreshold);
                float thirstMult = 1f + hotRatio * (_config.Temperature.MaxThirstAccel - 1f);
                _v.Thirst[i] = MathF.Max(0f, _v.Thirst[i]
                    - _config.Thirst.DecayRate * thirstMult);
            }
        }
    }

    private void ApplyScentPhysics()
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        int n = _v.N;

        float scentDecay = _config.Scent.DecayRate;
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
                _v.ScentGrid[x, y] *= scentDecay;

        float diffRate = _config.Scent.DiffusionRate;
        if (diffRate > 0f)
        {
            Array.Clear(_scentBuf, 0, _scentBuf.Length);
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    float s = _v.ScentGrid[x, y];
                    float share = s * diffRate;
                    if (x > 0) _scentBuf[(x - 1) * H + y] += share * 0.25f;
                    if (x < W - 1) _scentBuf[(x + 1) * H + y] += share * 0.25f;
                    if (y > 0) _scentBuf[x * H + (y - 1)] += share * 0.25f;
                    if (y < H - 1) _scentBuf[x * H + (y + 1)] += share * 0.25f;
                    _scentBuf[x * H + y] += s * (1f - diffRate);
                }
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                    _v.ScentGrid[x, y] = _scentBuf[x * H + y];
        }

        float foodScentDecay = _config.FoodScent.DecayRate;
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
                _v.FoodScentGrid[x, y] *= foodScentDecay;

        float foodDiffRate = _config.FoodScent.DiffusionRate;
        if (foodDiffRate > 0f)
        {
            Array.Clear(_foodScentBuf, 0, _foodScentBuf.Length);
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    float s = _v.FoodScentGrid[x, y];
                    float share = s * foodDiffRate;
                    if (x > 0) _foodScentBuf[(x - 1) * H + y] += share * 0.25f;
                    if (x < W - 1) _foodScentBuf[(x + 1) * H + y] += share * 0.25f;
                    if (y > 0) _foodScentBuf[x * H + (y - 1)] += share * 0.25f;
                    if (y < H - 1) _foodScentBuf[x * H + (y + 1)] += share * 0.25f;
                    _foodScentBuf[x * H + y] += s * (1f - foodDiffRate);
                }
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                    _v.FoodScentGrid[x, y] = _foodScentBuf[x * H + y];
        }

        for (int i = 0; i < n; i++)
        {
            if (!_v.Alive[i]) continue;
            _v.Hunger[i] = MathF.Max(0f, _v.Hunger[i] - _config.Hunger.DecayRate);
        }

        for (int i = 0; i < n; i++)
        {
            if (!_v.Alive[i]) continue;
            _v.Thirst[i] = MathF.Max(0f, _v.Thirst[i] - _config.Thirst.DecayRate);
        }

        for (int i = 0; i < n; i++)
        {
            if (!_v.Alive[i]) continue;
            _v.ChemicalMemory[i] *= _config.Chemical.DecayRate;
            _v.ChemicalAge[i]++;
        }
    }

    private void ApplyFoodDecay()
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;

        lock (_v.LockObj)
        {
            float foodDecay = _config.Grid.FoodDecayPerTick;
            for (int f = _v.FoodTiles.Count - 1; f >= 0; f--)
            {
                var food = _v.FoodTiles[f];
                float decay = MathF.Min(foodDecay, food.Energy);
                food.Energy -= decay;
                if (food.Energy <= 0)
                {
                    _v.NutrientGrid[food.X, food.Y] = MathF.Min(_config.Nutrient.MaxNutrient,
                        _v.NutrientGrid[food.X, food.Y] + decay * _config.Nutrient.PlantToNutrientRatio);
                    _v.FoodTiles.RemoveAt(f);
                }
                else
                {
                    _v.FoodTiles[f] = food;
                }
            }

            for (int c = _v.CorpseTiles.Count - 1; c >= 0; c--)
            {
                var corpse = _v.CorpseTiles[c];
                float decay = MathF.Min(_config.Corpse.DecayPerTick, corpse.Energy);
                corpse.Energy -= decay;
                if (corpse.Energy <= 0)
                    _v.CorpseTiles.RemoveAt(c);
                else
                {
                    _v.CorpseTiles[c] = corpse;
                    if (corpse.X < W && corpse.Y < H)
                        _v.FoodScentGrid[corpse.X, corpse.Y] += _config.Corpse.ScentAmount;
                }
                _v.NutrientGrid[corpse.X, corpse.Y] = MathF.Min(_config.Nutrient.MaxNutrient,
                    _v.NutrientGrid[corpse.X, corpse.Y] + decay * _config.Nutrient.CorpseToNutrientRatio);
            }
        }
    }

    private void ApplyNutrientDiffusion()
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        float rate = _config.Nutrient.DiffusionRate;
        float max = _config.Nutrient.MaxNutrient;

        var newGrid = new float[W, H];
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                float current = _v.NutrientGrid[x, y];
                float outflow = current * rate;
                float inflow = 0f;
                int nc = 0;
                if (x > 0) { inflow += _v.NutrientGrid[x - 1, y] * rate; nc++; }
                if (x < W - 1) { inflow += _v.NutrientGrid[x + 1, y] * rate; nc++; }
                if (y > 0) { inflow += _v.NutrientGrid[x, y - 1] * rate; nc++; }
                if (y < H - 1) { inflow += _v.NutrientGrid[x, y + 1] * rate; nc++; }
                newGrid[x, y] = MathF.Min(current - outflow + inflow / MathF.Max(1, nc), max);
            }
        _v.NutrientGrid = newGrid;
    }

    /// <summary>
    /// Diffuse chemical field across 4-neighbor grid with decay.
    /// New diffusion: c[x,y] = decay * (c[x,y] + rate * sum(neighbors - c[x,y]) / 4)
    /// </summary>
    private void ApplyChemicalDiffusion()
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        float rate = _config.Chemical.DiffusionRate;
        float decay = _config.Chemical.DecayRate;

        var newField = new float[W, H];
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                float cur = _v.ChemicalField[x, y];
                float neighborSum = 0f;
                int count = 0;
                if (x > 0) { neighborSum += _v.ChemicalField[x - 1, y]; count++; }
                if (x < W - 1) { neighborSum += _v.ChemicalField[x + 1, y]; count++; }
                if (y > 0) { neighborSum += _v.ChemicalField[x, y - 1]; count++; }
                if (y < H - 1) { neighborSum += _v.ChemicalField[x, y + 1]; count++; }
                float avgNeighbor = count > 0 ? neighborSum / count : cur;
                newField[x, y] = (cur + rate * (avgNeighbor - cur)) * decay;
            }
        _v.ChemicalField = newField;
    }
}
