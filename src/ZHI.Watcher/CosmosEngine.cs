using System.Text.Json;
using ZHI.Core;
using ZHI.Shared;
using TorchSharp;
using static TorchSharp.torch;

namespace ZHI.Watcher;

/// <summary>
/// 空间化网格生态系统引擎 — 128×128 grid, 7 actions, Stress combat, Scent trails
/// </summary>
public class CosmosEngine : IDisposable
{
    private readonly ZhiConfig _config;
    private readonly Blackbox _blackbox;
    private readonly string _configPath;
    private readonly string _logFilePath;
    private readonly StreamWriter _logWriter;
    private CancellationTokenSource _cts = new();
    private readonly Random _rng = new();

    private VectorizedState _v = null!;
    private GRUBrain _gruBrain = null!;
    private RNDModule? _rnd;
    private MAPElitesGrid? _mapElites;
    private PPOBuffer? _ppoBuffer;
    private Tensor? _gruHidden;

    private const int PpoRolloutSteps = 64;
    private const float PpoClipEpsilon = 0.2f;
    private const float PpoEntropyCoef = 0.01f;
    private const float GaeLambda = 0.95f;

    private readonly List<byte[]> _agentWeights = new();
    private int _generation;
    private int _totalDeaths;
    private int _globalTick;
    private bool _paused;
    private float _effectiveMutationRate;

    private readonly List<GenerationResult> _genResults = new();

    public event Action<string>? OnLog;
    public event Action? OnStateChanged;

    public int Generation => _generation;
    public int TotalDeaths => _totalDeaths;
    public int AgentCount => _v?.N ?? 0;
    public bool Paused => _paused;
    public VectorizedState State => _v;

    // PLACEHOLDER_CONSTRUCTOR

    public CosmosEngine(ZhiConfig config, Blackbox blackbox, string configPath)
    {
        _config = config;
        _blackbox = blackbox;
        _configPath = configPath;

        ZHI.Core.Device.Initialize();

        var logDir = Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? ".";
        _logFilePath = Path.Combine(logDir, "watcher.log");
        _logWriter = new StreamWriter(_logFilePath, append: true, System.Text.Encoding.UTF8) { AutoFlush = true };

        int n = config.Cosmos.AgentCount;
        _v = new VectorizedState(n, ZHI.Core.Device.TorchDevice);
        _gruBrain = new GRUBrain(config.Network.LearningRate, config.Network.Gamma);
        _rnd = new RNDModule(learningRate: 0.0005f, rewardScale: 0.05f);
        _mapElites = new MAPElitesGrid();
        _ppoBuffer = new PPOBuffer(PpoRolloutSteps, n, ZHI.Core.Device.TorchDevice);
        _gruHidden = torch.zeros(1, n, _gruBrain.HiddenSize, device: ZHI.Core.Device.TorchDevice);

        _generation = config.DeathCount + 1;
        _totalDeaths = config.DeathCount;

        Log($"[Cosmos] Grid world init: {n} agents, gen {_generation}, device {ZHI.Core.Device.Name}");
    }

    public async Task RunAsync()
    {
        try
        {
            InitializeGeneration(loadWeights: null);

            while (!_cts.Token.IsCancellationRequested)
            {
                if (!_paused)
                {
                    try { Tick(); }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Cosmos] Tick error: {ex}");
                        Log($"[Cosmos] Tick error: {ex}");
                        break;
                    }
                }
                try { await Task.Delay(_config.DecisionIntervalMs, _cts.Token); }
                catch (OperationCanceledException) { break; }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Cosmos] Fatal: {ex}");
            Log($"[Cosmos] Fatal: {ex}");
        }
    }

    private void InitializeGeneration(List<byte[]>? loadWeights)
    {
        _genResults.Clear();
        _globalTick = 0;
        int n = _v.N;

        // Reset agent state
        for (int i = 0; i < n; i++)
        {
            _v.PosX[i] = _rng.Next(ToolDefinitions.GridWidth);
            _v.PosY[i] = _rng.Next(ToolDefinitions.GridHeight);
            _v.Existence[i] = _config.Existence.Initial;
            _v.Stress[i] = 0f;
            _v.Alive[i] = true;
            _v.LastAction[i] = 0;
            _v.LastSignalReceived[i] = -1;
            for (int ch = 0; ch < ToolDefinitions.SignalValues; ch++)
                _v.SignalMemory[i, ch] = 0f;
            _v.TickCount[i] = 0;
            _v.AttackCount[i] = 0;
            _v.EatCount[i] = 0;
            _v.SignalCount[i] = 0;
            _v.StatusMirror[i] = "ALIVE";
            _v.LastActionNameMirror[i] = "";
            _v.BirthTimes[i] = DateTime.UtcNow;
        }

        // Reset grid
        _v.FoodTiles.Clear();
        Array.Clear(_v.ScentGrid);
        SpawnInitialFood();

        // Load brain weights
        if (loadWeights != null && loadWeights.Count > 0)
        {
            _gruBrain.LoadWeights(loadWeights[0]);
        }
        else
        {
            _gruBrain.Dispose();
            _gruBrain = new GRUBrain(_config.Network.LearningRate, _config.Network.Gamma);
        }

        _gruHidden?.Dispose();
        _gruHidden = torch.zeros(1, n, _gruBrain.HiddenSize, device: _v.Device);
        _ppoBuffer?.Clear();

        _agentWeights.Clear();
        for (int i = 0; i < n; i++)
        {
            if (loadWeights != null && i < loadWeights.Count)
                _agentWeights.Add(loadWeights[i]);
            else
                _agentWeights.Add(_gruBrain.SaveWeights());
        }

        Log($"[Cosmos] Gen {_generation} initialized");
    }

    // PLACEHOLDER_TICK

    private void Tick()
    {
        _globalTick++;
        int n = _v.N;

        // 1. Stress damage: Existence -= Stress * StressDamage
        for (int i = 0; i < n; i++)
        {
            if (!_v.Alive[i]) continue;
            _v.Existence[i] -= _v.Stress[i] * _config.Combat.StressDamage;
        }

        // 2. Stress decay
        for (int i = 0; i < n; i++)
        {
            if (!_v.Alive[i]) continue;
            _v.Stress[i] = MathF.Max(0f, _v.Stress[i] - _config.Combat.StressDecay);
        }

        // 3. Existence natural decay
        for (int i = 0; i < n; i++)
        {
            if (!_v.Alive[i]) continue;
            _v.Existence[i] -= _config.Existence.DecayPerTick;
        }

        // 4. Scent decay
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        float scentDecay = _config.Scent.DecayRate;
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
                _v.ScentGrid[x, y] *= scentDecay;

        // 4b. Signal memory decay
        for (int i = 0; i < n; i++)
        {
            if (!_v.Alive[i]) continue;
            for (int ch = 0; ch < ToolDefinitions.SignalValues; ch++)
                _v.SignalMemory[i, ch] *= 0.9f;
        }

        // 5. Food TTL & spawning + food scent emission
        for (int f = _v.FoodTiles.Count - 1; f >= 0; f--)
        {
            var food = _v.FoodTiles[f];
            food.TTL--;
            if (food.TTL <= 0)
                _v.FoodTiles.RemoveAt(f);
            else
            {
                _v.FoodTiles[f] = food;
                _v.ScentGrid[food.X, food.Y] += 0.3f;
            }
        }
        TrySpawnFood();

        // 6. Build 37-dim state
        _v.BuildStateMatrix();

        // 7. GRU inference
        var (actions, signalValues, logProbs, values, entropy, newHidden) =
            _gruBrain.StepForward(_v.StateMatrix, _gruHidden);
        _gruHidden?.Dispose();
        _gruHidden = newHidden;

        // Extract to CPU
        long[] actionsArr = new long[n];
        actions.cpu().data<long>().CopyTo(actionsArr);
        long[] signalArr = new long[n];
        signalValues.cpu().data<long>().CopyTo(signalArr);
        float[] logProbsArr = new float[n];
        logProbs.cpu().data<float>().CopyTo(logProbsArr);
        float[] valuesArr = new float[n];
        values.cpu().data<float>().CopyTo(valuesArr);

        // 8. Process actions
        float[] rewards = new float[n];
        ProcessActions(actionsArr, signalArr, rewards);

        // 9. Death check
        float[] donesArr = new float[n];
        for (int i = 0; i < n; i++)
        {
            if (!_v.Alive[i]) continue;
            if (_v.Existence[i] <= 0f)
            {
                _v.Alive[i] = false;
                _v.StatusMirror[i] = "DEAD";
                rewards[i] = -20f;
                donesArr[i] = 1f;
            }
        }

        // Survival reward for alive agents
        for (int i = 0; i < n; i++)
        {
            if (_v.Alive[i])
                rewards[i] += 0.1f;
        }

        // RND intrinsic curiosity
        using (var scope = torch.NewDisposeScope())
        {
            using var intrinsic = _rnd!.ComputeIntrinsicReward(_v.StateMatrix);
            float[] intrinsicArr = new float[n];
            intrinsic.cpu().data<float>().CopyTo(intrinsicArr);
            for (int i = 0; i < n; i++)
                if (_v.Alive[i]) rewards[i] += intrinsicArr[i];
        }

        // 10. Update tracking
        for (int i = 0; i < n; i++)
        {
            if (_v.Alive[i] || donesArr[i] == 1f)
            {
                _v.LastAction[i] = actionsArr[i];
                _v.LastActionNameMirror[i] = ToolDefinitions.ActionNames[(int)actionsArr[i]];
                _v.TickCount[i]++;
            }
        }

        // Reset GRU hidden for dead agents
        using (var dScope = torch.NewDisposeScope())
        {
            var aliveMask = new float[n];
            for (int i = 0; i < n; i++) aliveMask[i] = _v.Alive[i] ? 1f : 0f;
            using var mask = tensor(aliveMask, device: _v.Device).unsqueeze(0).unsqueeze(-1);
            _gruHidden!.mul_(mask);
        }

        // 11. Store in PPO buffer
        float[] stateFlat = _v.GetStateBuffer();
        _ppoBuffer!.Store(stateFlat, actionsArr, signalArr, logProbsArr, rewards, donesArr, valuesArr);

        // 12. PPO update
        if (_ppoBuffer.IsFull)
        {
            var (advArr, retArr) = _ppoBuffer.ComputeGAE(_config.Network.Gamma, GaeLambda);
            var tensors = _ppoBuffer.ToTensors(advArr, retArr);
            if (tensors != null)
            {
                var (states, acts, sigs, oldLP, adv, ret) = tensors.Value;
                float loss = _gruBrain.PpoUpdate(states, acts, sigs, oldLP, adv, ret,
                    epochs: 4, clipEpsilon: PpoClipEpsilon, entCoef: PpoEntropyCoef);
                float rndLoss = _rnd!.Train(states);
                Log($"[PPO] loss={loss:F4} rnd={rndLoss:F4}");
                states.Dispose(); acts.Dispose(); sigs.Dispose();
                oldLP.Dispose(); adv.Dispose(); ret.Dispose();
            }
            _ppoBuffer.Clear();
        }

        // 13. Check all dead → next generation
        int aliveCount = 0;
        for (int i = 0; i < n; i++) if (_v.Alive[i]) aliveCount++;
        if (aliveCount == 0 && n > 0)
            EndGeneration();

        // Cleanup
        actions.Dispose(); signalValues.Dispose();
        logProbs.Dispose(); values.Dispose(); entropy.Dispose();

        OnStateChanged?.Invoke();
    }

    // PLACEHOLDER_ACTIONS

    private void ProcessActions(long[] actions, long[] signalValues, float[] rewards)
    {
        int n = _v.N;
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;

        // Collect eat attempts for BigFood cooperative resolution
        var eatAttempts = new List<int>();

        for (int i = 0; i < n; i++)
        {
            if (!_v.Alive[i]) continue;

            switch ((ZhiAction)actions[i])
            {
                case ZhiAction.MoveUp:
                    if (_v.PosY[i] > 0) _v.PosY[i]--;
                    _v.ScentGrid[_v.PosX[i], _v.PosY[i]] += _config.Scent.DepositAmount;
                    break;

                case ZhiAction.MoveDown:
                    if (_v.PosY[i] < H - 1) _v.PosY[i]++;
                    _v.ScentGrid[_v.PosX[i], _v.PosY[i]] += _config.Scent.DepositAmount;
                    break;

                case ZhiAction.MoveLeft:
                    if (_v.PosX[i] > 0) _v.PosX[i]--;
                    _v.ScentGrid[_v.PosX[i], _v.PosY[i]] += _config.Scent.DepositAmount;
                    break;

                case ZhiAction.MoveRight:
                    if (_v.PosX[i] < W - 1) _v.PosX[i]++;
                    _v.ScentGrid[_v.PosX[i], _v.PosY[i]] += _config.Scent.DepositAmount;
                    break;

                case ZhiAction.Eat:
                    eatAttempts.Add(i);
                    break;

                case ZhiAction.Attack:
                    ProcessAttack(i);
                    break;

                case ZhiAction.Signal:
                    ProcessSignal(i, (int)signalValues[i]);
                    break;
            }
        }

        // Resolve eat attempts (normal food: solo; BigFood: need multiple agents)
        ResolveEatAttempts(eatAttempts, rewards);
    }

    private void ResolveEatAttempts(List<int> eaters, float[] rewards)
    {
        var consumed = new HashSet<int>();
        int minAgents = _config.Grid.BigFoodMinAgents;

        // Group eaters by position
        var byPos = new Dictionary<(int x, int y), List<int>>();
        foreach (int i in eaters)
        {
            var pos = (_v.PosX[i], _v.PosY[i]);
            if (!byPos.TryGetValue(pos, out var list))
            {
                list = new List<int>();
                byPos[pos] = list;
            }
            list.Add(i);
        }

        foreach (var (pos, agents) in byPos)
        {
            // Find food at this position
            int foodIdx = -1;
            for (int f = 0; f < _v.FoodTiles.Count; f++)
            {
                if (consumed.Contains(f)) continue;
                if (_v.FoodTiles[f].X == pos.x && _v.FoodTiles[f].Y == pos.y)
                {
                    foodIdx = f;
                    break;
                }
            }

            if (foodIdx < 0)
            {
                // No food here — all eaters fail
                foreach (int i in agents)
                {
                    _v.Existence[i] -= _config.Existence.EatFailPenalty;
                    rewards[i] -= 1.0f;
                }
                continue;
            }

            var tile = _v.FoodTiles[foodIdx];

            if (tile.IsBig)
            {
                if (agents.Count >= minAgents)
                {
                    // Cooperative eat succeeds — all participants get bonus
                    consumed.Add(foodIdx);
                    foreach (int i in agents)
                    {
                        _v.Existence[i] += _config.Grid.BigFoodBonus;
                        _v.EatCount[i]++;
                        rewards[i] += 8.0f;
                    }
                }
                else
                {
                    // Not enough agents — eat fails but no penalty (they tried)
                    foreach (int i in agents)
                        rewards[i] -= 0.5f;
                }
            }
            else
            {
                // Normal food — first eater gets it
                consumed.Add(foodIdx);
                int winner = agents[0];
                _v.Existence[winner] += _config.Existence.EatBonus;
                _v.EatCount[winner]++;
                rewards[winner] += 3.0f;

                // Others at same position fail
                for (int k = 1; k < agents.Count; k++)
                {
                    _v.Existence[agents[k]] -= _config.Existence.EatFailPenalty;
                    rewards[agents[k]] -= 1.0f;
                }
            }
        }

        // Remove consumed food tiles (reverse order)
        var sorted = consumed.OrderByDescending(x => x).ToList();
        foreach (int idx in sorted)
            _v.FoodTiles.RemoveAt(idx);
    }

    private void ProcessAttack(int attacker)
    {
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

        if (bestTarget >= 0)
        {
            _v.Stress[bestTarget] += _config.Combat.StressPerAttack;
            _v.LastActionNameMirror[attacker] = $"attack#{bestTarget}";
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

        for (int j = 0; j < _v.N; j++)
        {
            if (j == signaler || !_v.Alive[j]) continue;
            int dx = Math.Abs(_v.PosX[j] - sx);
            int dy = Math.Abs(_v.PosY[j] - sy);
            if (dx <= R && dy <= R)
            {
                _v.LastSignalReceived[j] = signalValue;
                _v.SignalMemory[j, signalValue] = 1.0f;
            }
        }
    }

    // PLACEHOLDER_FOOD

    private void SpawnInitialFood()
    {
        int count = _config.Grid.MaxFood / 2;
        for (int i = 0; i < count; i++)
            TrySpawnOneFood();
    }

    private void TrySpawnFood()
    {
        if (_v.FoodTiles.Count >= _config.Grid.MaxFood) return;
        if (_rng.NextDouble() < _config.Grid.FoodSpawnChance)
            TrySpawnOneFood();
    }

    private void TrySpawnOneFood()
    {
        int W = ToolDefinitions.GridWidth, H = ToolDefinitions.GridHeight;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            int x = _rng.Next(W);
            int y = _rng.Next(H);
            bool occupied = false;
            for (int f = 0; f < _v.FoodTiles.Count; f++)
            {
                if (_v.FoodTiles[f].X == x && _v.FoodTiles[f].Y == y)
                { occupied = true; break; }
            }
            if (!occupied)
            {
                bool isBig = _rng.NextDouble() < _config.Grid.BigFoodChance;
                _v.FoodTiles.Add(new FoodTile { X = x, Y = y, TTL = _config.Grid.FoodTTL, IsBig = isBig });
                return;
            }
        }
    }

    private void EndGeneration()
    {
        Log($"[Cosmos] Gen {_generation} all dead, tick {_globalTick}");
        int n = _v.N;

        _totalDeaths += n;
        _config.DeathCount = _totalDeaths;
        SaveConfig();

        for (int i = 0; i < n; i++)
        {
            double aliveSecs = (DateTime.UtcNow - _v.BirthTimes[i]).TotalSeconds;
            double fitness = aliveSecs * (1.0 + _v.EatCount[i] * 0.2 + _v.AttackCount[i] * 0.05);

            _genResults.Add(new GenerationResult
            {
                AgentId = i,
                Weights = _agentWeights[i],
                Fitness = fitness,
                AliveSeconds = aliveSecs,
                Cause = _v.StatusMirror[i],
                AttackCount = _v.AttackCount[i],
                TickCount = _v.TickCount[i],
                EatCount = _v.EatCount[i],
                SignalCount = _v.SignalCount[i]
            });

            var record = new DeathRecord
            {
                Generation = _generation,
                Cause = _v.StatusMirror[i],
                CpuAtDeath = _v.Stress[i],
                MemAtDeath = 0f,
                ExistenceAtDeath = _v.Existence[i],
                PreDeathStatesJson = JsonSerializer.Serialize(new { PosX = _v.PosX[i], PosY = _v.PosY[i], Stress = _v.Stress[i] }),
                DeathTime = DateTime.UtcNow,
                AliveSeconds = aliveSecs
            };
            _blackbox.RecordDeath(record);
            _blackbox.SaveAgentWeights(_generation, i, _agentWeights[i]);
        }

        // Adaptive mutation
        var decayGen = Math.Max(1, _config.Cosmos.MutationDecayGenerations);
        var progress = Math.Min(1f, (float)_generation / decayGen);
        _effectiveMutationRate = _config.Cosmos.MutationRateMin
            + (_config.Cosmos.MutationRate - _config.Cosmos.MutationRateMin) * (1f - progress);

        // MAP-Elites
        foreach (var r in _genResults)
        {
            float ticks = Math.Max(1, r.TickCount);
            float aggression = Math.Clamp((float)r.AttackCount / ticks, 0f, 1f);
            float exploration = Math.Clamp((float)r.EatCount / ticks, 0f, 1f);
            _mapElites!.TryInsert(r.Weights, r.Fitness, aggression, exploration);
        }

        // Genetic selection
        var sorted = _genResults.OrderByDescending(r => r.Fitness).ToList();
        var nextWeights = new List<byte[]>();
        int eliteCount = _config.Cosmos.EliteCount;

        for (int i = 0; i < n; i++)
        {
            if (i < eliteCount)
            {
                var w = sorted[i].Weights;
                if (_rng.NextDouble() < 0.3) w = MutateOnly(w);
                nextWeights.Add(w);
            }
            else
            {
                var pa = _mapElites!.SampleParent(_rng);
                var pb = _mapElites.SampleParent(_rng);
                if (pa != null && pb != null)
                    nextWeights.Add(Breed(pa.Value.Weights, pb.Value.Weights));
                else
                {
                    var p1 = sorted[_rng.Next(Math.Min(4, sorted.Count))];
                    var p2 = sorted[_rng.Next(Math.Min(4, sorted.Count))];
                    nextWeights.Add(Breed(p1.Weights, p2.Weights));
                }
            }
        }

        var best = sorted[0];
        _blackbox.SaveWeights(_generation, best.Weights);
        Log($"[Gen] best={best.Fitness:F1} alive={best.AliveSeconds:F1}s eat={best.EatCount} atk={best.AttackCount}");

        _generation++;
        InitializeGeneration(nextWeights);
    }

    // PLACEHOLDER_GENETIC

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

    private void SaveConfig()
    {
        var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_configPath, json);
    }

    public void TogglePause() => _paused = !_paused;
    public void RequestShutdown() { Log("[Cosmos] Shutting down..."); _cts.Cancel(); }

    public void Dispose()
    {
        _cts.Cancel();
        _v?.Dispose();
        _gruBrain?.Dispose();
        _rnd?.Dispose();
        _gruHidden?.Dispose();
        _ppoBuffer?.Dispose();
        _logWriter?.Flush();
        _logWriter?.Dispose();
        _blackbox.Dispose();
        _cts.Dispose();
    }

    private void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        OnLog?.Invoke(line);
        try { _logWriter?.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}"); } catch { }
    }
}

public class GenerationResult
{
    public int AgentId { get; set; }
    public byte[] Weights { get; set; } = Array.Empty<byte>();
    public double Fitness { get; set; }
    public double AliveSeconds { get; set; }
    public string Cause { get; set; } = "";
    public int AttackCount { get; set; }
    public int TickCount { get; set; }
    public int EatCount { get; set; }
    public int SignalCount { get; set; }
}
