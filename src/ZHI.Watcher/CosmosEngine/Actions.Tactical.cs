using ZHI.Shared;

namespace ZHI.Watcher;

public partial class CosmosEngine
{
    private void ProcessAttack(int attacker)
    {
        if (_globalTick - _lastAttackTick[attacker] < 4)
        {
            _v.LastActionNameMirror[attacker] = "attack_cooldown";
            return;
        }

        int range = _config.Combat.AttackRange;
        int ax = _v.PosX[attacker], ay = _v.PosY[attacker];
        int bestTarget = -1;
        int bestDist = int.MaxValue;

        for (int j = 0; j < _v.N; j++)
        {
            if (j == attacker || !_v.Alive[j]) continue;
            int dist = Math.Abs(_v.PosX[j] - ax) + Math.Abs(_v.PosY[j] - ay);
            if (dist <= range && dist < bestDist)
            {
                bestDist = dist;
                bestTarget = j;
            }
        }

        _v.Existence[attacker] -= _config.Combat.AttackCost;
        _v.AttackCount[attacker]++;
        _v.Stamina[attacker] = MathF.Max(0f, _v.Stamina[attacker] - _config.Stamina.AttackCost);

        if (bestTarget >= 0)
        {
            float hpRatio = Math.Clamp(_v.Existence[attacker] / 100f, 0.2f, 1.5f);
            float damage = 20f * hpRatio;
            byte attackerTerrain = _v.TerrainType[ax, ay];
            if (attackerTerrain == ToolDefinitions.TerrainPit) damage *= 0.8f;
            else if (attackerTerrain == ToolDefinitions.TerrainMound) damage *= 1.1f;
            if (_v.IsEating[bestTarget]) damage *= 1.1f;
            if (_v.IsStationary[bestTarget]) damage *= _config.Stamina.StationaryDamageMult;
            _v.Stress[bestTarget] += _config.Combat.StressPerAttack;
            _v.Existence[bestTarget] -= damage;
            _v.LastActionNameMirror[attacker] = $"attack#{bestTarget}";
            _lastAttackTick[attacker] = _globalTick;
            _tickEvents.Add(new WorldEvent
            {
                Type = "attack", AgentId = attacker, TargetId = bestTarget,
                Value = damage, Tick = _globalTick
            });
            _genAttacks++;
        }
        else
        {
            _v.LastActionNameMirror[attacker] = "attack_miss";
        }
    }

    private void ProcessSignal(int signaler, int signalValue)
    {
        int R = ToolDefinitions.VisionRadius;
        int sx = _v.PosX[signaler], sy = _v.PosY[signaler];

        _v.Existence[signaler] -= _config.Signal.Cost;
        _v.SignalCount[signaler]++;

        _tickEvents.Add(new WorldEvent
        {
            Type = "signal", AgentId = signaler,
            SignalValue = signalValue, Tick = _globalTick
        });

        for (int j = 0; j < _v.N; j++)
        {
            if (j == signaler || !_v.Alive[j]) continue;
            int dx = Math.Abs(_v.PosX[j] - sx);
            int dy = Math.Abs(_v.PosY[j] - sy);
            if (dx <= R && dy <= R)
            {
                _v.LastSignalReceived[j] = signalValue;
                _v.SignalMemory[j, signalValue] = 1.0f;
                _v.SignalAge[j] = 0;
            }
        }

        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        if (sx >= 0 && sx < W && sy >= 0 && sy < H && signalValue >= 0 && signalValue < 4)
            _v.SignalField[sx, sy, signalValue] = MathF.Min(1.0f,
                _v.SignalField[sx, sy, signalValue] + 0.5f);
    }

    private void ProcessPush(int i, float[] rewards)
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        int px = _v.PosX[i], py = _v.PosY[i];
        int fd = _v.FacingDirection[i];

        if (_v.Stamina[i] < _config.Stamina.LowStaminaThreshold)
            return;

        int fx = px, fy = py;
        switch (fd)
        {
            case 0: fy = py - 1; break;
            case 1: fy = py + 1; break;
            case 2: fx = px - 1; break;
            default: fx = px + 1; break;
        }
        if (fx < 0 || fx >= W || fy < 0 || fy >= H) { _v.Stamina[i] -= _config.Stamina.PushCost * 0.5f; return; }

        bool hasEntity = false;
        int entityType = 0;
        int foodIdx = -1, corpseIdx = -1;

        lock (_v.LockObj)
        {
            for (int f = 0; f < _v.FoodTiles.Count; f++)
            {
                var ft = _v.FoodTiles[f];
                int fw = ft.Width > 0 ? ft.Width : 1;
                int fh = ft.Height > 0 ? ft.Height : 1;
                if (fx >= ft.X && fx < ft.X + fw && fy >= ft.Y && fy < ft.Y + fh)
                {
                    hasEntity = true; entityType = 1; foodIdx = f; break;
                }
            }
            if (!hasEntity)
            {
                for (int c = 0; c < _v.CorpseTiles.Count; c++)
                {
                    var ct = _v.CorpseTiles[c];
                    if (ct.X == fx && ct.Y == fy)
                    {
                        hasEntity = true; entityType = 2; corpseIdx = c; break;
                    }
                }
            }

            if (!hasEntity) { _v.Stamina[i] -= _config.Stamina.PushCost * 0.5f; return; }

            int dx = fx, dy = fy;
            switch (fd)
            {
                case 0: dy = fy - 1; break;
                case 1: dy = fy + 1; break;
                case 2: dx = fx - 1; break;
                default: dx = fx + 1; break;
            }

            if (dx < 0 || dx >= W || dy < 0 || dy >= H) { _v.Stamina[i] -= _config.Stamina.PushCost * 0.5f; return; }
            if (_v.IsDeepWater(dx, dy)) { _v.Stamina[i] -= _config.Stamina.PushCost * 0.5f; return; }
            if (_v.IsMoundAt(dx, dy)) { _v.Stamina[i] -= _config.Stamina.PushCost * 0.5f; return; }
            if (_v.HasAnyAgentAt(dx, dy)) { _v.Stamina[i] -= _config.Stamina.PushCost * 0.5f; return; }

            bool destOccupied = false;
            foreach (var ft in _v.FoodTiles)
            {
                int fw2 = ft.Width > 0 ? ft.Width : 1;
                int fh2 = ft.Height > 0 ? ft.Height : 1;
                if (dx >= ft.X && dx < ft.X + fw2 && dy >= ft.Y && dy < ft.Y + fh2)
                { destOccupied = true; break; }
            }
            if (!destOccupied)
            {
                foreach (var ct in _v.CorpseTiles)
                    if (ct.X == dx && ct.Y == dy) { destOccupied = true; break; }
            }
            if (destOccupied) { _v.Stamina[i] -= _config.Stamina.PushCost * 0.5f; return; }

            if (entityType == 1 && foodIdx >= 0)
            {
                var ft = _v.FoodTiles[foodIdx];
                if (ft.IsBig) { _v.Stamina[i] -= _config.Stamina.PushCost * 0.5f; return; }
                ft.X = dx; ft.Y = dy;
                _v.FoodTiles[foodIdx] = ft;
            }
            else if (entityType == 2 && corpseIdx >= 0)
            {
                var ct = _v.CorpseTiles[corpseIdx];
                ct.X = dx; ct.Y = dy;
                _v.CorpseTiles[corpseIdx] = ct;
            }

            _v.Stamina[i] = MathF.Max(0f, _v.Stamina[i] - _config.Stamina.PushCost);
            _v.PushCount[i]++;
            _tickEvents.Add(new WorldEvent
            {
                Type = "push", AgentId = i,
                FoodType = entityType == 1 ? "food" : "corpse",
                Tick = _globalTick
            });
        }
    }

    private void ProcessTerraform(int i, float[] rewards)
    {
        int px = _v.PosX[i], py = _v.PosY[i];

        if (_v.Stamina[i] < _config.Stamina.LowStaminaThreshold)
            return;

        if (_v.RiverGrid[px, py] > 0)
        {
            _v.Stamina[i] = MathF.Max(0f, _v.Stamina[i] - _config.Stamina.TerraformCost * 0.5f);
            return;
        }

        byte current = _v.GetTerrainAt(px, py);
        byte next;

        if (current == ToolDefinitions.TerrainDynamicWater)
            next = ToolDefinitions.TerrainMound;
        else if (current == ToolDefinitions.TerrainFlat)
            next = ToolDefinitions.TerrainPit;
        else if (current == ToolDefinitions.TerrainPit)
            next = ToolDefinitions.TerrainMound;
        else
            next = ToolDefinitions.TerrainFlat;

        if (next == ToolDefinitions.TerrainMound && _v.HasAnyAgentAt(px, py))
        {
            _v.Stamina[i] = MathF.Max(0f, _v.Stamina[i] - _config.Stamina.TerraformCost * 0.5f);
            return;
        }

        bool wasWater = current == ToolDefinitions.TerrainDynamicWater;
        _v.TerrainType[px, py] = next;
        if (next == ToolDefinitions.TerrainPit || next == ToolDefinitions.TerrainMound)
            _v.TerrainTTL[px, py] = ToolDefinitions.TerrainTTL;
        else
            _v.TerrainTTL[px, py] = 0;

        _v.Stamina[i] = MathF.Max(0f, _v.Stamina[i] - _config.Stamina.TerraformCost);
        _v.TerraformCount[i]++;

        string terrainName = next == ToolDefinitions.TerrainPit ? "pit" :
                             next == ToolDefinitions.TerrainMound ? "mound" : "flat";
        _tickEvents.Add(new WorldEvent
        {
            Type = wasWater ? "dam_built" : "terraform",
            AgentId = i,
            FoodType = terrainName,
            Tick = _globalTick
        });
    }
}
