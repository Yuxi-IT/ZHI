using ZHI.Shared;

namespace ZHI.Watcher;

public partial class CosmosEngine
{
    private void ApplyWorldTemperature(int n)
    {
        _gameTimeOfDay = ((_globalTick + 1200) % 3600) / 150f;
        float hourAngle = (_gameTimeOfDay - 8f) * MathF.PI / 12f;
        _temperature = 20f + 15f * MathF.Sin(hourAngle);

        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
                _v.TemperatureGrid[x, y] = _temperature;

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

            _v.TemperatureGrid[ax, ay] += agentHeat + selfBonus;
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = ax + dx, ny = ay + dy;
                    if (nx >= 0 && nx < W && ny >= 0 && ny < H)
                        _v.TemperatureGrid[nx, ny] += agentHeat * 0.5f + neighborBonus;
                }
        }

        float riverCooling = _config.Temperature.RiverCooling;
        int riverCoolRange = _config.Temperature.RiverCoolingRange;
        if (riverCooling > 0f && riverCoolRange > 0)
        {
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    int rv = _v.RiverGrid[x, y];
                    if (rv == 0) continue;
                    float depthFactor = rv == 2 ? 1f : 0.5f;
                    for (int dx = -riverCoolRange; dx <= riverCoolRange; dx++)
                        for (int dy = -riverCoolRange; dy <= riverCoolRange; dy++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || nx >= W || ny < 0 || ny >= H) continue;
                            int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                            float falloff = 1f - (float)dist / (riverCoolRange + 1);
                            _v.TemperatureGrid[nx, ny] -= riverCooling * depthFactor * falloff;
                        }
                }
        }

        bool isDaytime = _gameTimeOfDay >= 6f && _gameTimeOfDay < 20f;
        if (isDaytime)
        {
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                    if (_v.TerrainType[x, y] == ToolDefinitions.TerrainMound)
                        _v.TemperatureGrid[x, y] -= 3f;
        }
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
            float localTemp = _v.TemperatureGrid[_v.PosX[i], _v.PosY[i]];
            _v.BodyTemperature[i] += (localTemp - _v.BodyTemperature[i]) * BodyTempLerpRate;
        }

        for (int i = 0; i < n; i++)
        {
            if (!_v.Alive[i]) continue;
            float bodyTemp = _v.BodyTemperature[i];

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
            for (int ch = 0; ch < ToolDefinitions.SignalValues; ch++)
                _v.SignalMemory[i, ch] *= 0.9f;
            _v.SignalAge[i]++;
        }
    }

    private void ApplyFoodDecayAndRespawn()
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;

        lock (_v.LockObj)
        {
            float foodDecay = _config.Grid.FoodDecayPerTick;
            float bigFoodDecay = _config.Grid.BigFoodDecayPerTick;
            for (int f = _v.FoodTiles.Count - 1; f >= 0; f--)
            {
                var food = _v.FoodTiles[f];
                float decay = food.IsBig ? bigFoodDecay : foodDecay;
                food.Energy -= decay;
                if (food.Energy <= 0)
                    _v.FoodTiles.RemoveAt(f);
                else
                {
                    _v.FoodTiles[f] = food;
                    float scentAmount = food.IsBig ? _config.FoodScent.BigFoodEmission : _config.FoodScent.SmallFoodEmission;
                    int fw = food.Width > 0 ? food.Width : 1;
                    int fh = food.Height > 0 ? food.Height : 1;
                    int spreadRadius = _config.FoodScent.SpreadRadius;
                    for (int cellX = 0; cellX < fw; cellX++)
                        for (int cellY = 0; cellY < fh; cellY++)
                        {
                            int cx = food.X + cellX;
                            int cy = food.Y + cellY;
                            for (int dx = -spreadRadius; dx <= spreadRadius; dx++)
                                for (int dy = -spreadRadius; dy <= spreadRadius; dy++)
                                {
                                    int sx = cx + dx;
                                    int sy = cy + dy;
                                    if (sx >= 0 && sx < W && sy >= 0 && sy < H)
                                    {
                                        float dist = MathF.Sqrt(dx * dx + dy * dy);
                                        float falloff = MathF.Max(0, 1f - dist / (spreadRadius + 1));
                                        _v.FoodScentGrid[sx, sy] += scentAmount * falloff;
                                    }
                                }
                        }
                }
            }

            for (int c = _v.CorpseTiles.Count - 1; c >= 0; c--)
            {
                var corpse = _v.CorpseTiles[c];
                corpse.Energy -= _config.Corpse.DecayPerTick;
                if (corpse.Energy <= 0)
                    _v.CorpseTiles.RemoveAt(c);
                else
                {
                    _v.CorpseTiles[c] = corpse;
                    if (corpse.X < W && corpse.Y < H)
                        _v.FoodScentGrid[corpse.X, corpse.Y] += _config.Corpse.ScentAmount;
                }
            }
        }

        if (_config.Grid.FoodRespawnInterval > 0
            && _globalTick % _config.Grid.FoodRespawnInterval == 0)
        {
            lock (_v.LockObj)
            {
                if (_v.FoodTiles.Count < _config.Grid.MaxFood)
                {
                    for (int attempt = 0; attempt < 20; attempt++)
                    {
                        int rx = _rng.Next(W);
                        int ry = _rng.Next(H);
                        if (!IsValidFoodPlacement(rx, ry, 1, 1)) continue;

                        _v.FoodTiles.Add(new FoodTile
                        {
                            X = rx, Y = ry,
                            Width = 1, Height = 1,
                            Energy = _config.Grid.FoodEnergy,
                            IsBig = false
                        });
                        break;
                    }
                }
            }
        }
    }
}
