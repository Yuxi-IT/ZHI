using ZHI.Shared;

namespace ZHI.Watcher;

public partial class CosmosEngine
{
    private void ApplyPlantGrowth()
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        bool isDaytime = _gameTimeOfDay >= 6f && _gameTimeOfDay < 20f;

        float tempFactor = ComputeGrowthTempFactor();
        float growthRate = _config.Plant.BaseGrowthRate;
        float maxEnergy = _config.Plant.MaxPlantEnergy;
        float plantDecay = _config.Grid.FoodDecayPerTick;
        float minTemp = _config.Plant.MinTemp;
        float maxTemp = _config.Plant.MaxTemp;
        float deathTemp = _config.Plant.DeathTemp;

        // Build plant presence lookup
        var plantGrid = new bool[W, H];
        lock (_v.LockObj)
            foreach (var ft in _v.FoodTiles)
                if (ft.X >= 0 && ft.X < W && ft.Y >= 0 && ft.Y < H)
                    plantGrid[ft.X, ft.Y] = true;

        var newGrowth = new List<FoodTile>();

        lock (_v.LockObj)
        {
            // 1. Grow existing plants + kill dead ones
            for (int f = _v.FoodTiles.Count - 1; f >= 0; f--)
            {
                var plant = _v.FoodTiles[f];
                int x = plant.X, y = plant.Y;
                float cellTemp = _v.TemperatureGrid[x, y];

                // Death by freezing
                if (cellTemp < deathTemp)
                {
                    _v.NutrientGrid[x, y] = MathF.Min(_config.Nutrient.MaxNutrient,
                        _v.NutrientGrid[x, y] + plant.Energy * _config.Nutrient.PlantToNutrientRatio);
                    _v.FoodTiles.RemoveAt(f);
                    plantGrid[x, y] = false;
                    continue;
                }

                // Death by overheating
                if (cellTemp > maxTemp)
                {
                    _v.FoodTiles.RemoveAt(f);
                    plantGrid[x, y] = false;
                    continue;
                }

                // No growth if too cold or at night
                if (cellTemp < minTemp || !isDaytime) continue;

                float waterFactor = MathF.Min(1f, _v.GroundwaterGrid[x, y] / _config.Plant.WaterNeed);
                float nutrientFactor = MathF.Min(1f, _v.NutrientGrid[x, y] / _config.Plant.NutrientNeed);

                float growth = growthRate * tempFactor * waterFactor * nutrientFactor;
                growth = MathF.Min(growth, maxEnergy - plant.Energy);

                if (growth > 0)
                {
                    plant.Energy += growth;
                    _v.GroundwaterGrid[x, y] = MathF.Max(0, _v.GroundwaterGrid[x, y] - growth * _config.Plant.WaterConsumption);
                    _v.NutrientGrid[x, y] = MathF.Max(0, _v.NutrientGrid[x, y] - growth * _config.Plant.NutrientConsumption);
                }

                // Natural decay
                plant.Energy -= plantDecay;
                if (plant.Energy <= 0)
                {
                    _v.NutrientGrid[x, y] = MathF.Min(_config.Nutrient.MaxNutrient,
                        _v.NutrientGrid[x, y] + 0.5f);
                    _v.FoodTiles.RemoveAt(f);
                    plantGrid[x, y] = false;
                }
                else
                {
                    _v.FoodTiles[f] = plant;
                }
            }

            // 2. Plant spreading (seed dispersal with wind drift)
            var spreadCandidates = new List<FoodTile>(_v.FoodTiles);
            float seedWindDrift = _config.Wind.SeedWindDrift;
            foreach (var plant in spreadCandidates)
            {
                if (plant.Energy < maxEnergy * 0.5f) continue;

                float spreadChance = _config.Plant.SpreadChance * tempFactor;
                if (_rng.NextDouble() > spreadChance) continue;

                int radius = _config.Plant.SpreadRadius;
                // Wind-biased dispersal: blend random direction with wind direction
                float wx = _v.WindX[plant.X, plant.Y], wy = _v.WindY[plant.X, plant.Y];
                float windMag = MathF.Sqrt(wx * wx + wy * wy);
                float windDirX = windMag > 0.01f ? wx / windMag : 0f;
                float windDirY = windMag > 0.01f ? wy / windMag : 0f;

                for (int attempt = 0; attempt < 3; attempt++)
                {
                    int dx = _rng.Next(-radius, radius + 1);
                    int dy = _rng.Next(-radius, radius + 1);
                    // Drift toward downwind direction
                    dx += (int)MathF.Round(windDirX * radius * seedWindDrift * (float)_rng.NextDouble());
                    dy += (int)MathF.Round(windDirY * radius * seedWindDrift * (float)_rng.NextDouble());
                    if (dx == 0 && dy == 0) continue;
                    int nx = plant.X + dx, ny = plant.Y + dy;
                    if (nx < 0 || nx >= W || ny < 0 || ny >= H) continue;
                    if (plantGrid[nx, ny]) continue;
                    if (_v.RiverGrid[nx, ny] > 0) continue;
                    if (_v.SurfaceWaterGrid[nx, ny] > 0.5f) continue;

                    float nTemp = _v.TemperatureGrid[nx, ny];
                    if (nTemp < minTemp || nTemp > maxTemp) continue;
                    if (_v.GroundwaterGrid[nx, ny] < _config.Plant.WaterNeed * 0.5f) continue;
                    if (_v.NutrientGrid[nx, ny] < _config.Plant.NutrientNeed * 0.3f) continue;

                    newGrowth.Add(new FoodTile { X = nx, Y = ny, Energy = 1f });
                    plantGrid[nx, ny] = true;
                    _v.NutrientGrid[nx, ny] = MathF.Max(0, _v.NutrientGrid[nx, ny] - 0.1f);
                    break;
                }
            }
        }

        if (newGrowth.Count > 0)
        {
            lock (_v.LockObj)
                _v.FoodTiles.AddRange(newGrowth);
        }

        // 3. Propagate food scent from plants (proportional to energy)
        int spreadRadius = _config.FoodScent.SpreadRadius;
        float baseEmission = _config.FoodScent.SmallFoodEmission;
        for (int i = 0; i < _v.FoodTiles.Count; i++)
        {
            var ft = _v.FoodTiles[i];
            float emission = baseEmission * (ft.Energy / maxEnergy);
            for (int dx = -spreadRadius; dx <= spreadRadius; dx++)
                for (int dy = -spreadRadius; dy <= spreadRadius; dy++)
                {
                    int sx = ft.X + dx, sy = ft.Y + dy;
                    if (sx < 0 || sx >= W || sy < 0 || sy >= H) continue;
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    float falloff = MathF.Max(0, 1f - dist / (spreadRadius + 1));
                    _v.FoodScentGrid[sx, sy] += emission * falloff;
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
