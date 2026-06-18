using ZHI.Shared;

namespace ZHI.Watcher;

public partial class CosmosEngine
{
    private void ApplyPlantGrowth()
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        bool isDaytime = _gameTimeOfDay >= 6f && _gameTimeOfDay < 20f;
        var cfg = _config.Plant;

        float tempFactor = ComputeGrowthTempFactor();

        // Build plant presence lookup
        var plantGrid = new bool[W, H];
        lock (_v.LockObj)
            foreach (var p in _v.Plants)
                if (p.X >= 0 && p.X < W && p.Y >= 0 && p.Y < H)
                    plantGrid[p.X, p.Y] = true;

        var newPlants = new List<PlantTile>();

        lock (_v.LockObj)
        {
            for (int f = _v.Plants.Count - 1; f >= 0; f--)
            {
                var plant = _v.Plants[f];
                int x = plant.X, y = plant.Y;
                var stage = (PlantStage)plant.Stage;
                float cellTemp = _v.TemperatureGrid[x, y];
                float gw = _v.GroundwaterGrid[x, y];
                float nu = _v.NutrientGrid[x, y];
                float sun = _v.Sunlight[x, y];
                plant.Age++;

                switch (stage)
                {
                    case PlantStage.Seed:
                        ProcessSeed(ref plant, x, y, cellTemp, gw, nu, sun, cfg);
                        break;
                    case PlantStage.Sprout:
                        ProcessSprout(ref plant, x, y, cellTemp, gw, nu, sun, cfg, tempFactor, isDaytime);
                        break;
                    case PlantStage.Adult:
                        ProcessAdult(ref plant, x, y, cellTemp, gw, nu, sun, cfg, tempFactor, isDaytime, plantGrid, newPlants);
                        break;
                    case PlantStage.Decay:
                        ProcessDecay(ref plant, x, y, cfg);
                        break;
                }

                // Remove dead plants
                if (plant.Energy <= 0f)
                {
                    _v.NutrientGrid[x, y] = MathF.Min(_config.Nutrient.MaxNutrient,
                        _v.NutrientGrid[x, y] + 0.5f * cfg.DecayNutrientReturn);
                    _v.Plants.RemoveAt(f);
                    plantGrid[x, y] = false;
                }
                else
                {
                    _v.Plants[f] = plant;
                    // Update spatial lookup grid
                    _v.UpdatePlantCell(x, y, plant.Energy, plant.Stage);
                }
            }

            // Add new seeds from adult reproduction
            if (newPlants.Count > 0)
                _v.Plants.AddRange(newPlants);
        }

        // Propagate food scent (skip seeds and deep decay)
        PropagateFoodScent(cfg.MaxPlantEnergy);
    }

    private void ProcessSeed(ref PlantTile plant, int x, int y,
        float cellTemp, float gw, float nu, float sun, PlantConfig cfg)
    {
        // Seeds are dormant and hardy — only extreme temperatures kill them
        if (cellTemp < cfg.DeathTemp || cellTemp > cfg.MaxTemp)
        {
            plant.Energy -= 0.01f;
            return;
        }

        // Germination check: need nutrients, water, sunlight, and enough age
        if (plant.Age < cfg.SeedGermDelay) return;
        if (nu < cfg.SeedGermNutrientMin || gw < cfg.SeedGermWaterMin || sun <= 0f) return;

        // Germinate!
        plant.Stage = (byte)PlantStage.Sprout;
        plant.Health = cfg.SproutInitialHealth;
        plant.Age = 0;
    }

    private void ProcessSprout(ref PlantTile plant, int x, int y,
        float cellTemp, float gw, float nu, float sun, PlantConfig cfg,
        float tempFactor, bool isDaytime)
    {
        // Death by extreme temperature (sprouts are more vulnerable)
        if (cellTemp < cfg.MinTemp || cellTemp > cfg.MaxTemp - 5f)
        {
            plant.Health -= cfg.SproutHealthLoss * 3f;
            if (plant.Health <= 0f) { plant.Stage = (byte)PlantStage.Decay; return; }
            return;
        }
        if (cellTemp < cfg.DeathTemp + 3f)
        {
            plant.Energy = 0f;
            return;
        }

        // Health update: recover in good conditions, degrade in stress
        float waterFactor = MathF.Min(1f, gw / cfg.WaterNeed);
        float nutrientFactor = MathF.Min(1f, nu / cfg.NutrientNeed);
        if (waterFactor > 0.7f && nutrientFactor > 0.5f && cellTemp > cfg.MinTemp + 2f)
            plant.Health = MathF.Min(1f, plant.Health + 0.01f);
        else
            plant.Health -= cfg.SproutHealthLoss;

        if (plant.Health <= 0f)
        {
            plant.Stage = (byte)PlantStage.Decay;
            return;
        }

        // Growth
        if (cellTemp < cfg.MinTemp || !isDaytime) return;

        float sunFactor = 1f + sun * _config.Sunlight.SunPhotosynthesisBoost;
        float biomeFactor = GetBiomePlantGrowthMult(x, y);
        float growth = cfg.BaseGrowthRate * cfg.SproutGrowthMult * tempFactor
                       * waterFactor * nutrientFactor * sunFactor * biomeFactor;
        growth = MathF.Min(growth, cfg.MaxPlantEnergy - plant.Energy);

        if (growth > 0)
        {
            plant.Energy += growth;
            _v.GroundwaterGrid[x, y] = MathF.Max(0, gw - growth * cfg.WaterConsumption);
            _v.NutrientGrid[x, y] = MathF.Max(0, nu - growth * cfg.NutrientConsumption);
        }

        // Transition to adult
        if (plant.Energy >= cfg.SproutAdultEnergy)
        {
            plant.Stage = (byte)PlantStage.Adult;
            plant.Age = 0;
        }
    }

    private void ProcessAdult(ref PlantTile plant, int x, int y,
        float cellTemp, float gw, float nu, float sun, PlantConfig cfg,
        float tempFactor, bool isDaytime,
        bool[,] plantGrid, List<PlantTile> newPlants)
    {
        // Death by freezing
        if (cellTemp < cfg.DeathTemp)
        {
            plant.Energy = 0f;
            return;
        }
        // Death by overheating
        if (cellTemp > cfg.MaxTemp)
        {
            plant.Energy = 0f;
            return;
        }

        // Health update
        float waterFactor = MathF.Min(1f, gw / cfg.WaterNeed);
        float nutrientFactor = MathF.Min(1f, nu / cfg.NutrientNeed);
        if (waterFactor > 0.5f && nutrientFactor > 0.4f && cellTemp > cfg.MinTemp)
            plant.Health = MathF.Min(1f, plant.Health + 0.005f);
        else
            plant.Health -= 0.01f;

        // Forced aging: max age reached → decay
        if (plant.Age >= cfg.AdultMaxAge)
        {
            plant.Stage = (byte)PlantStage.Decay;
            return;
        }
        // Health failure → decay
        if (plant.Health <= 0f)
        {
            plant.Stage = (byte)PlantStage.Decay;
            return;
        }

        // Growth
        if (cellTemp >= cfg.MinTemp && isDaytime)
        {
            float sunFactor = 1f + sun * _config.Sunlight.SunPhotosynthesisBoost;
            float biomeFactor = GetBiomePlantGrowthMult(x, y);
            float growth = cfg.BaseGrowthRate * tempFactor
                           * waterFactor * nutrientFactor * sunFactor * biomeFactor;
            growth = MathF.Min(growth, cfg.MaxPlantEnergy - plant.Energy);

            if (growth > 0)
            {
                plant.Energy += growth;
                _v.GroundwaterGrid[x, y] = MathF.Max(0, gw - growth * cfg.WaterConsumption);
                _v.NutrientGrid[x, y] = MathF.Max(0, nu - growth * cfg.NutrientConsumption);
            }
        }

        // Natural decay
        plant.Energy -= _config.Grid.FoodDecayPerTick;
        if (plant.Energy <= 0) return;

        // Seed production: healthy adults with surplus energy spread seeds
        float seedThreshold = cfg.MaxPlantEnergy * 0.4f + cfg.AdultSeedCost;
        if (plant.Energy < seedThreshold || plant.Health < 0.5f) return;

        float spreadChance = cfg.SpreadChance * tempFactor;
        if (_rng.NextDouble() > spreadChance) return;

        TryProduceSeed(ref plant, cfg, plantGrid, newPlants);
    }

    private void ProcessDecay(ref PlantTile plant, int x, int y, PlantConfig cfg)
    {
        // Lose energy and return nutrients to soil
        float decayLoss = cfg.DecayRate * (1f + (1f - plant.Health) * 2f);
        float lost = MathF.Min(decayLoss, plant.Energy);
        plant.Energy -= lost;

        float heightNorm = _v.HeightMap[x, y] / 255f;
        float cellMax = _config.Nutrient.MaxNutrient * (1f - heightNorm * _config.Nutrient.HeightRetentionFactor);
        _v.NutrientGrid[x, y] = MathF.Min(cellMax,
            _v.NutrientGrid[x, y] + lost * cfg.DecayNutrientReturn);

        // Health continues to drop in decay
        plant.Health = MathF.Max(0f, plant.Health - 0.01f);
    }

    private void TryProduceSeed(ref PlantTile parent, PlantConfig cfg,
        bool[,] plantGrid, List<PlantTile> newPlants)
    {
        int W = ToolDefinitions.GridWidth, H = ToolDefinitions.GridHeight;
        int radius = cfg.SpreadRadius;

        // Wind-biased dispersal
        float wx = _v.WindX[parent.X, parent.Y], wy = _v.WindY[parent.X, parent.Y];
        float windMag = MathF.Sqrt(wx * wx + wy * wy);
        float windDirX = windMag > 0.01f ? wx / windMag : 0f;
        float windDirY = windMag > 0.01f ? wy / windMag : 0f;

        for (int attempt = 0; attempt < 3; attempt++)
        {
            int dx = _rng.Next(-radius, radius + 1);
            int dy = _rng.Next(-radius, radius + 1);
            dx += (int)MathF.Round(windDirX * radius * _config.Wind.SeedWindDrift * (float)_rng.NextDouble());
            dy += (int)MathF.Round(windDirY * radius * _config.Wind.SeedWindDrift * (float)_rng.NextDouble());
            if (dx == 0 && dy == 0) continue;

            int nx = parent.X + dx, ny = parent.Y + dy;
            if (nx < 0 || nx >= W || ny < 0 || ny >= H) continue;
            if (plantGrid[nx, ny]) continue;
            if (_v.RiverGrid[nx, ny] > 0) continue;
            if (_v.SurfaceWaterGrid[nx, ny] > 0.5f) continue;

            float nTemp = _v.TemperatureGrid[nx, ny];
            if (nTemp < cfg.MinTemp || nTemp > cfg.MaxTemp) continue;
            if (_v.GroundwaterGrid[nx, ny] < cfg.WaterNeed * 0.3f) continue;
            if (_v.NutrientGrid[nx, ny] < cfg.NutrientNeed * 0.2f) continue;

            // Pay energy cost and create seed
            parent.Energy -= cfg.AdultSeedCost;

            newPlants.Add(new PlantTile
            {
                X = nx, Y = ny,
                Energy = cfg.SeedInitialEnergy,
                Stage = (byte)PlantStage.Seed,
                Age = 0,
                Health = 1f
            });
            plantGrid[nx, ny] = true;
            _v.NutrientGrid[nx, ny] = MathF.Max(0, _v.NutrientGrid[nx, ny] - 0.05f);
            break;
        }
    }

    private void PropagateFoodScent(float maxEnergy)
    {
        int W = ToolDefinitions.GridWidth, H = ToolDefinitions.GridHeight;
        int spreadRadius = _config.FoodScent.SpreadRadius;
        float baseEmission = _config.FoodScent.SmallFoodEmission;

        lock (_v.LockObj)
        {
            foreach (var plant in _v.Plants)
            {
                var stage = (PlantStage)plant.Stage;
                // Seeds don't emit scent; Decay emits reduced scent
                if (stage == PlantStage.Seed) continue;
                float emission = baseEmission * (plant.Energy / maxEnergy);
                if (stage == PlantStage.Decay) emission *= 0.3f;
                if (stage == PlantStage.Sprout) emission *= 0.5f;

                for (int dx = -spreadRadius; dx <= spreadRadius; dx++)
                    for (int dy = -spreadRadius; dy <= spreadRadius; dy++)
                    {
                        int sx = plant.X + dx, sy = plant.Y + dy;
                        if (sx < 0 || sx >= W || sy < 0 || sy >= H) continue;
                        float dist = MathF.Sqrt(dx * dx + dy * dy);
                        float falloff = MathF.Max(0, 1f - dist / (spreadRadius + 1));
                        _v.FoodScentGrid[sx, sy] += emission * falloff;
                    }
            }
        }
    }

    private float ComputeGrowthTempFactor()
    {
        float opt = _config.Plant.OptimalTemp;
        float mn = _config.Plant.MinTemp;
        float mx = _config.Plant.MaxTemp;

        if (_temperature <= mn || _temperature >= mx) return 0f;
        if (_temperature <= opt)
            return (_temperature - mn) / (opt - mn);
        return (mx - _temperature) / (mx - opt);
    }
}
