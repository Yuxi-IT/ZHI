using System.Text.Json;
using ZHI.Core;
using ZHI.Shared;
using TorchSharp;
using static TorchSharp.torch;

namespace ZHI.Watcher;

public partial class CosmosEngine
{
    private int GetAliveCount()
    {
        int count = 0;
        for (int i = 0; i < _v.N; i++)
            if (_v.Alive[i]) count++;
        return count;
    }

    private int CountNearbyAgents(int agentIdx, int range)
    {
        int count = 0;
        int ax = _v.PosX[agentIdx], ay = _v.PosY[agentIdx];
        for (int j = 0; j < _v.N; j++)
        {
            if (j == agentIdx || !_v.Alive[j]) continue;
            if (Math.Abs(_v.PosX[j] - ax) + Math.Abs(_v.PosY[j] - ay) <= range)
                count++;
        }
        return count;
    }

    private void RespawnDeadAgents()
    {
        int delay = _config.Cosmos.RespawnDelayTicks;
        int n = _v.N;

        for (int i = 0; i < n; i++)
        {
            if (_v.Alive[i]) continue;
            if (_deathTick[i] < 0) continue;
            if (_globalTick - _deathTick[i] < delay) continue;

            double livedSecs = (_v.BirthTimes[i] != default)
                ? (DateTime.UtcNow - _v.BirthTimes[i]).TotalSeconds : 0;

            // Record death to DB
            var record = new DeathRecord
            {
                Generation = _generation,
                Cause = _v.StatusMirror[i],
                StressAtDeath = _v.Stress[i],
                ExistenceAtDeath = _v.Existence[i],
                HungerAtDeath = _v.Hunger[i],
                ThirstAtDeath = _v.Thirst[i],
                Temperature = _temperature,
                TimeOfDay = _gameTimeOfDay,
                PosX = _v.PosX[i],
                PosY = _v.PosY[i],
                AttackCount = _v.AttackCount[i],
                EatCount = _v.EatCount[i],
                EmitCount = _v.EmitCount[i],
                RespawnCount = _v.RespawnCount[i],
                PreDeathStatesJson = JsonSerializer.Serialize(new { PosX = _v.PosX[i], PosY = _v.PosY[i], Stress = _v.Stress[i] }),
                DeathTime = DateTime.UtcNow,
                AliveSeconds = livedSecs
            };
            _blackbox.RecordDeath(record);

            // Respawn with new random genome
            _v.RespawnAgent(i, _rng);
            var newGenome = Genome.Random(_rng, _config.Genome.MutationStd);
            _v.Genomes[i] = newGenome;
            _v.BodySize[i] = newGenome.Size;
            _v.BodySpeed[i] = newGenome.Speed;
            _v.BodyStrength[i] = newGenome.Strength;
            _v.BodyVision[i] = newGenome.VisionRange;
            _v.BodyFat[i] = newGenome.FatStorage;
            _v.BodyColdResist[i] = newGenome.ColdResistance;
            _deathTick[i] = -1;

            // Zero GRU hidden state for this agent
            if (_gruHidden is not null)
            {
                using (var _ = torch.no_grad())
                    _gruHidden[0, i].zero_();
            }

            _totalDeaths++;
            _respawnCount++;
            _tickEvents.Add(new WorldEvent { Type = "respawn", AgentId = i, Tick = _globalTick });
            Log($"[Cosmos] Agent #{i} respawned (lived {livedSecs:F1}s)");

            // Lightweight generation milestone: every 16 respawns = 1 generation
            if (_respawnCount % 16 == 0)
            {
                _generation++;
                _config.DeathCount = _totalDeaths;
                SaveConfig();

                // Save best weights every 8 generations (every 512 respawns)
                if (_generation % 8 == 0)
                {
                    int bestIdx = -1;
                    int bestTicks = 0;
                    for (int j = 0; j < _v.N; j++)
                    {
                        if (_v.Alive[j] && _v.TickCount[j] > bestTicks)
                        { bestIdx = j; bestTicks = _v.TickCount[j]; }
                    }
                    if (bestIdx >= 0 && bestIdx < _agentWeights.Count)
                        _blackbox.SaveWeights(_generation, _agentWeights[bestIdx]);

                    Log($"[Cosmos] ===== Gen {_generation} saved weights (respawn milestone, total deaths={_totalDeaths}) =====");
                }
                else
                {
                    Log($"[Cosmos] ===== Gen {_generation} (respawn milestone, total deaths={_totalDeaths}) =====");
                }
            }
        }
    }

    private int ReproduceAgent(int parentIdx)
    {
        // Find adjacent empty cell
        int px = _v.PosX[parentIdx];
        int py = _v.PosY[parentIdx];
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;

        int cx = px, cy = py;
        bool found = false;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            int dx = _rng.Next(-1, 2);
            int dy = _rng.Next(-1, 2);
            if (dx == 0 && dy == 0) continue;
            int nx = px + dx;
            int ny = py + dy;
            if (nx >= 0 && nx < W && ny >= 0 && ny < H
                && _v.GetCellOccupancy(nx, ny) < 2)
            {
                cx = nx; cy = ny; found = true; break;
            }
        }

        if (!found) return -1; // No empty adjacent cell

        // Parent pays cost
        _v.Existence[parentIdx] -= _config.Reproduce.ParentCost;

        // Create child with mutated brain and genome
        int childIdx = _v.AddAgent(cx, cy, _config.Reproduce.ChildStart);

        // Inherit and mutate parent's genome
        var parentGenome = _v.Genomes[parentIdx] ?? Genome.Random(_rng, _config.Genome.MutationStd);
        var childGenome = parentGenome.Mutate(_rng, _config.Genome.MutationStd);
        _v.Genomes[childIdx] = childGenome;
        _v.BodySize[childIdx] = childGenome.Size;
        _v.BodySpeed[childIdx] = childGenome.Speed;
        _v.BodyStrength[childIdx] = childGenome.Strength;
        _v.BodyVision[childIdx] = childGenome.VisionRange;
        _v.BodyFat[childIdx] = childGenome.FatStorage;
        _v.BodyColdResist[childIdx] = childGenome.ColdResistance;

        // Copy and mutate parent's weights
        byte[] parentWeights = _agentWeights[parentIdx];
        byte[] childWeights = MutateChild(parentWeights);

        // Ensure agentWeights list is large enough
        while (_agentWeights.Count <= childIdx)
            _agentWeights.Add(childWeights);

        Log($"[Reproduce] agent {parentIdx} → child {childIdx} at ({cx},{cy})");
        return childIdx;
    }

    private byte[] MutateChild(byte[] parentWeights)
    {
        using var brain = new GRUBrain();
        brain.load(new MemoryStream(parentWeights));

        foreach (var (_, param) in brain.named_parameters())
        {
            if (_rng.NextDouble() < _config.Reproduce.MutationScale)
            {
                using var noise = torch.randn(param.shape, device: ZHI.Core.Device.TorchDevice)
                    * _config.Cosmos.MutationStd * _config.Reproduce.MutationScale;
                using (var _ = torch.no_grad()) param.add_(noise);
            }
        }

        // Child weight noise: ε~N(0, 0.005) on all parameters
        using (torch.no_grad())
        {
            foreach (var param in brain.parameters())
            {
                param.add_(torch.randn_like(param) * 0.005f);
            }
        }

        using var ms = new MemoryStream();
        brain.save(ms);
        return ms.ToArray();
    }

    private byte[] Breed(byte[] weightsA, byte[] weightsB)
    {
        using var brainA = new GRUBrain();
        using var brainB = new GRUBrain();
        brainA.load(new MemoryStream(weightsA));
        brainB.load(new MemoryStream(weightsB));
        using var child = new GRUBrain();

        foreach (var (name, childParam) in child.named_parameters())
        {
            var paramA = brainA.named_parameters().First(x => x.name == name).parameter;
            var paramB = brainB.named_parameters().First(x => x.name == name).parameter;

            using var mask = torch.randint(0, 2, paramA.shape, dtype: float32, device: ZHI.Core.Device.TorchDevice);
            using var invMask = 1.0f - mask;
            using var crossed = paramA * mask + paramB * invMask;

            if (_rng.NextDouble() < _effectiveMutationRate)
            {
                using var noise = torch.randn(paramA.shape, device: ZHI.Core.Device.TorchDevice) * _config.Cosmos.MutationStd;
                using var noised = crossed + noise;
                using (var _ = torch.no_grad()) childParam.copy_(noised);
            }
            else
            {
                using (var _ = torch.no_grad()) childParam.copy_(crossed);
            }
        }

        using var ms = new MemoryStream();
        child.save(ms);
        return ms.ToArray();
    }

    private byte[] MutateOnly(byte[] weights)
    {
        using var brain = new GRUBrain();
        brain.load(new MemoryStream(weights));

        foreach (var (_, param) in brain.named_parameters())
        {
            if (_rng.NextDouble() < _effectiveMutationRate * 0.5)
            {
                using var noise = torch.randn(param.shape, device: ZHI.Core.Device.TorchDevice) * _config.Cosmos.MutationStd * 0.5f;
                using (var _ = torch.no_grad()) param.add_(noise);
            }
        }

        using var ms = new MemoryStream();
        brain.save(ms);
        return ms.ToArray();
    }
}
