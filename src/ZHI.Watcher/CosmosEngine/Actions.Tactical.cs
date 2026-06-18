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

        _v.AttackCount[attacker]++;
        float attackCost = _config.Metabolism.AttackCostBase / _v.BodyStrength[attacker];
        _v.Energy[attacker] = MathF.Max(0f, _v.Energy[attacker] - attackCost);

        if (bestTarget >= 0)
        {
            float hpRatio = Math.Clamp(_v.Energy[attacker] / 100f, 0.2f, 1.5f);
            float damage = 20f * hpRatio * _v.BodyStrength[attacker];
            byte attackerTerrain = _v.TerrainType[ax, ay];
            if (attackerTerrain == ToolDefinitions.TerrainPit) damage *= 0.8f;
            else if (attackerTerrain == ToolDefinitions.TerrainMound) damage *= 1.1f;
            if (_v.IsEating[bestTarget]) damage *= 1.1f;
            if (_v.IsStationary[bestTarget]) damage *= _config.Metabolism.StationaryDamageMult;
            _v.Stress[bestTarget] += _config.Combat.StressPerAttack;
            _v.Energy[bestTarget] -= damage;
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

    /// <summary>
    /// Emit a continuous chemical signal (0–1) into the environment.
    /// Spreads via Chebyshev wave with linear falloff. Nearby agents pick up the chemical memory.
    /// </summary>
    private void ProcessEmitChemical(int emitter, float emissionValue)
    {
        float energy = _v.Energy[emitter];
        float cost = _config.Metabolism.EmitCost;
        if (energy < cost) return;

        // Scale emission by agent's fat storage (more fat = stronger signal)
        float scaledEmission = Math.Clamp(emissionValue * (0.5f + _v.BodyFat[emitter] * 0.5f), 0f, 1f);

        _v.Energy[emitter] = MathF.Max(0f, energy - cost);
        _v.EmitCount[emitter]++;

        _tickEvents.Add(new WorldEvent
        {
            Type = "signal", AgentId = emitter,
            SignalValue = (int)(scaledEmission * 100), Tick = _globalTick
        });

        int sx = _v.PosX[emitter], sy = _v.PosY[emitter];
        int R = _config.Chemical.WaveRadius;
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;

        // Spatial diffusion: Chebyshev radius R with linear falloff
        for (int dx = -R; dx <= R; dx++)
        {
            int cx = sx + dx;
            if (cx < 0 || cx >= W) continue;
            for (int dy = -R; dy <= R; dy++)
            {
                int cy = sy + dy;
                if (cy < 0 || cy >= H) continue;
                int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                float intensity = (float)(R - dist + 1) / (R + 1);
                _v.ChemicalField[cx, cy] = MathF.Min(1.0f,
                    _v.ChemicalField[cx, cy] + scaledEmission * intensity);
            }
        }

        // Agent-to-agent broadcast within wave radius
        for (int j = 0; j < _v.N; j++)
        {
            if (j == emitter || !_v.Alive[j]) continue;
            int dx = Math.Abs(_v.PosX[j] - sx);
            int dy = Math.Abs(_v.PosY[j] - sy);
            if (dx <= R && dy <= R)
            {
                _v.LastSignalReceived[j] = (int)(scaledEmission * 100);
                _v.ChemicalMemory[j] = MathF.Max(_v.ChemicalMemory[j], scaledEmission);
                _v.ChemicalAge[j] = 0;
            }
        }
    }
}
