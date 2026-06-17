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
    private int _tickExceptionCount;
    private bool _paused;
    private float _effectiveMutationRate;
    private float _totalEnergyInWorld;

    // Event broadcasting
    private readonly List<WorldEvent> _tickEvents = new();
    public IReadOnlyList<WorldEvent> TickEvents => _tickEvents;

    // Generation statistics
    private int _genAttacks;
    private int _genHidingTicks;
    private int _genFoodEaten;
    private int _genBigFoodEaten;
    private int _genCorpsesEaten;
    private int _genTotalTicks;
    private float _genFoodEnergy;
    private float _genBigFoodEnergy;
    private float _genCorpseEnergy;

    public int GenAttacks => _genAttacks;
    public int GenHidingTicks => _genHidingTicks;
    public int GenFoodEaten => _genFoodEaten;
    public int GenBigFoodEaten => _genBigFoodEaten;
    public int GenCorpsesEaten => _genCorpsesEaten;
    public int GenTotalTicks => _genTotalTicks;
    public float GenFoodEnergy => _genFoodEnergy;
    public float GenBigFoodEnergy => _genBigFoodEnergy;
    public float GenCorpseEnergy => _genCorpseEnergy;

    private readonly List<GenerationResult> _genResults = new();

    public event Action<string>? OnLog;
    public event Action? OnStateChanged;

    public int Generation => _generation;
    public int TotalDeaths => _totalDeaths;
    public int AgentCount => _v?.N ?? 0;
    public bool Paused => _paused;
    public VectorizedState State => _v;
    public float TotalEnergyInWorld => _totalEnergyInWorld;
    public int TickExceptionCount => _tickExceptionCount;

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
                        _tickExceptionCount++;
                        Console.WriteLine($"[Cosmos] Tick error #{_tickExceptionCount}: {ex}");
                        Log($"[Cosmos] Tick error #{_tickExceptionCount}: {ex.Message}");
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
        _genAttacks = 0;
        _genHidingTicks = 0;
        _genFoodEaten = 0;
        _genBigFoodEaten = 0;
        _genCorpsesEaten = 0;
        _genTotalTicks = 0;
        _genFoodEnergy = 0;
        _genBigFoodEnergy = 0;
        _genCorpseEnergy = 0;

        // Recreate state with configured agent count (resets any reproduction growth)
        int n = _config.Cosmos.AgentCount;
        _v.Dispose();
        _v = new VectorizedState(n, ZHI.Core.Device.TorchDevice);

        // Recreate PPO buffer for correct agent count
        _ppoBuffer?.Dispose();
        _ppoBuffer = new PPOBuffer(PpoRolloutSteps, n, ZHI.Core.Device.TorchDevice);

        // Reset grid
        Array.Clear(_v.ScentGrid);
        Array.Clear(_v.RiverGrid);
        Array.Clear(_v.WaterSoundGrid);
        GenerateRiver();
        ComputeWaterSound();
        SpawnInitialFood();

        // Relocate agents that spawned on deep water, initialize physiological state
        for (int i = 0; i < n; i++)
        {
            while (_v.IsDeepWater(_v.PosX[i], _v.PosY[i]))
            {
                _v.PosX[i] = _rng.Next(ToolDefinitions.GridWidth);
                _v.PosY[i] = _rng.Next(ToolDefinitions.GridHeight);
            }
            _v.Thirst[i] = _config.Thirst.Initial;
            _v.Hunger[i] = _config.Hunger.Initial;
        }

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

    // PLACEHOLDER_TICK

    private void Tick()
    {
        _globalTick++;
        int n = _v.N;
        _tickEvents.Clear();
        _genTotalTicks++;

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

        // 3. HP decay: BaseDecay + HungerPenalty + ThirstPenalty + AgeDeath
        for (int i = 0; i < n; i++)
        {
            if (!_v.Alive[i]) continue;
            float decay = _config.Existence.DecayPerTick;

            // Graduated age death
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

            // Hide reduction
            if (_v.IsHiding[i])
            {
                decay *= _config.Hide.DecayMultiplier;
                _genHidingTicks++;
            }

            // Hunger penalty (below threshold → HP drain)
            if (_v.Hunger[i] < _config.Hunger.PenaltyThreshold)
                decay += _config.Hunger.PenaltyAmount;

            // Thirst penalty (below threshold → HP drain, higher priority)
            if (_v.Thirst[i] < _config.Thirst.PenaltyThreshold)
                decay += _config.Thirst.PenaltyAmount;

            _v.Existence[i] -= decay;

            // Passive HP recovery when well-fed AND hydrated
            if (_v.Hunger[i] > 80f && _v.Thirst[i] > 80f)
                _v.Existence[i] = MathF.Min(_v.Existence[i] + 0.2f, _config.Existence.Initial);
        }

        // 4. Scent decay
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        float scentDecay = _config.Scent.DecayRate;
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
                _v.ScentGrid[x, y] *= scentDecay;

        // 4a. Hunger decay (no auto-eat)
        for (int i = 0; i < n; i++)
        {
            if (!_v.Alive[i]) continue;
            _v.Hunger[i] = MathF.Max(0f, _v.Hunger[i] - _config.Hunger.DecayRate);
        }

        // 4b. Thirst decay (no auto-drink)
        for (int i = 0; i < n; i++)
        {
            if (!_v.Alive[i]) continue;
            _v.Thirst[i] = MathF.Max(0f, _v.Thirst[i] - _config.Thirst.DecayRate);
        }

        // 4b. Signal memory decay + signal age
        for (int i = 0; i < n; i++)
        {
            if (!_v.Alive[i]) continue;
            for (int ch = 0; ch < ToolDefinitions.SignalValues; ch++)
                _v.SignalMemory[i, ch] *= 0.9f;
            _v.SignalAge[i]++;
        }

        // 5. Food TTL decay + food scent emission
        lock (_v.LockObj)
        {
            for (int f = _v.FoodTiles.Count - 1; f >= 0; f--)
            {
                var food = _v.FoodTiles[f];
                food.TTL--;
                if (food.TTL <= 0)
                    _v.FoodTiles.RemoveAt(f);
                else
                {
                    _v.FoodTiles[f] = food;
                    float scentAmount = food.IsBig ? 1.0f : 0.3f;
                    int fw = food.Width > 0 ? food.Width : 1;
                    int fh = food.Height > 0 ? food.Height : 1;
                    for (int fx = 0; fx < fw; fx++)
                        for (int fy = 0; fy < fh; fy++)
                        {
                            int sx = food.X + fx;
                            int sy = food.Y + fy;
                            if (sx < W && sy < H)
                                _v.ScentGrid[sx, sy] += scentAmount;
                        }
                }
            }

            // Corpse TTL decay + scent emission
            for (int c = _v.CorpseTiles.Count - 1; c >= 0; c--)
            {
                var corpse = _v.CorpseTiles[c];
                corpse.TTL--;
                if (corpse.TTL <= 0)
                    _v.CorpseTiles.RemoveAt(c);
                else
                {
                    _v.CorpseTiles[c] = corpse;
                    if (corpse.X < W && corpse.Y < H)
                        _v.ScentGrid[corpse.X, corpse.Y] += _config.Corpse.ScentAmount;
                }
            }
        }

        // 5b. Food respawn: unconditional fixed-rate continuous spawn
        if (_config.Grid.FoodRespawnInterval > 0
            && _globalTick % _config.Grid.FoodRespawnInterval == 0)
        {
            int rx = _rng.Next(W);
            int ry = _rng.Next(H);
            lock (_v.LockObj)
                _v.FoodTiles.Add(new FoodTile
                {
                    X = rx, Y = ry,
                    Width = 1, Height = 1,
                    TTL = _config.Grid.FoodTTL,
                    Energy = _config.Grid.FoodEnergy,
                    IsBig = false
                });
        }

        // 6. Rebuild spatial grids + build state
        _v.RebuildSpatialGrids();
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

        // 8b. Signal field decay (after all deposits this tick)
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
                for (int ch = 0; ch < 4; ch++)
                    _v.SignalField[x, y, ch] *= 0.9f;

        // 9. Death check + corpse spawning
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

                _tickEvents.Add(new WorldEvent { Type = "death", AgentId = i, Tick = _globalTick });
                if (_v.IsHiding[i]) _v.IsHiding[i] = false;

                // Spawn corpse at death position
                lock (_v.LockObj)
                    _v.CorpseTiles.Add(new CorpseTile
                    {
                        X = _v.PosX[i],
                        Y = _v.PosY[i],
                        TTL = _config.Corpse.TTL,
                        Energy = _config.Corpse.Energy
                    });
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

        // 13. Reproduction (asexual: Existence>threshold + Age>threshold → split)
        // Runs after PPO store so new agents start fresh next tick
        int preReproCount = n;
        int aliveNow = GetAliveCount();
        for (int i = 0; i < preReproCount; i++)
        {
            if (!_v.Alive[i]) continue;
            if (_v.Existence[i] >= _config.Reproduce.MinExistence
                && _v.TickCount[i] >= _config.Reproduce.MinAge
                && aliveNow < _config.Grid.MaxAgents
                && (_globalTick - _v.LastReproduceTick[i]) >= _config.Reproduce.Cooldown)
            {
                // Population pressure: reduce reproduction chance when overcrowded
                if (aliveNow > 128 && _rng.NextDouble() > 0.2) continue;
                int childIdx = ReproduceAgent(i);
                if (childIdx >= 0)
                {
                    _tickEvents.Add(new WorldEvent { Type = "reproduce", AgentId = i, ChildId = childIdx, Tick = _globalTick });
                    _v.LastReproduceTick[i] = _globalTick;
                }
            }
        }

        // Recreate buffers if agents increased from reproduction
        if (_v.N != n)
        {
            n = _v.N;
            var oldHidden = _gruHidden;
            _gruHidden = torch.zeros(1, n, _gruBrain!.HiddenSize, device: _v.Device);
            using (var _ = torch.no_grad())
                _gruHidden!.narrow(1, 0, (int)oldHidden!.shape[1]).copy_(oldHidden);
            oldHidden.Dispose();

            // Recreate PPO buffer for new agent count (discard partial rollout)
            _ppoBuffer?.Dispose();
            _ppoBuffer = new PPOBuffer(PpoRolloutSteps, n, _v.Device);
        }

        // 14. Check all dead or timeout → next generation
        int aliveCount = 0;
        for (int i = 0; i < n; i++) if (_v.Alive[i]) aliveCount++;
        bool timedOut = _globalTick > 5000 && aliveCount < 3;
        if ((aliveCount == 0 || timedOut) && n > 0)
        {
            if (timedOut)
                Log($"[Cosmos] Gen {_generation} timed out at tick {_globalTick}, alive={aliveCount}");
            EndGeneration();
        }

        // Compute total energy in world
        _totalEnergyInWorld = 0f;
        lock (_v.LockObj)
        {
            foreach (var f in _v.FoodTiles) _totalEnergyInWorld += f.Energy;
            foreach (var c in _v.CorpseTiles) _totalEnergyInWorld += c.Energy;
        }
        for (int i = 0; i < n; i++)
            if (_v.Alive[i]) _totalEnergyInWorld += Math.Max(0, _v.Existence[i]);

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

            var action = (ZhiAction)actions[i];

            // Auto-cancel hide on move/eat/attack (if min duration met)
            if (_v.IsHiding[i] && action != ZhiAction.Hide && action != ZhiAction.Signal)
            {
                if (_globalTick - _v.HideStartTick[i] >= _config.Hide.MinDuration)
                {
                    _v.IsHiding[i] = false;
                    _tickEvents.Add(new WorldEvent { Type = "hide_exit", AgentId = i, Tick = _globalTick });
                }
                else
                {
                    // Forced to stay hidden — skip action, just maintain hide
                    action = ZhiAction.Hide;
                }
            }

            switch (action)
            {
                case ZhiAction.MoveUp:
                    if (_v.PosY[i] > 0 && !_v.IsDeepWater(_v.PosX[i], _v.PosY[i] - 1))
                        _v.PosY[i]--;
                    _v.ScentGrid[_v.PosX[i], _v.PosY[i]] += _config.Scent.DepositAmount;
                    _v.FacingDirection[i] = 0;
                    break;

                case ZhiAction.MoveDown:
                    if (_v.PosY[i] < H - 1 && !_v.IsDeepWater(_v.PosX[i], _v.PosY[i] + 1))
                        _v.PosY[i]++;
                    _v.ScentGrid[_v.PosX[i], _v.PosY[i]] += _config.Scent.DepositAmount;
                    _v.FacingDirection[i] = 1;
                    break;

                case ZhiAction.MoveLeft:
                    if (_v.PosX[i] > 0 && !_v.IsDeepWater(_v.PosX[i] - 1, _v.PosY[i]))
                        _v.PosX[i]--;
                    _v.ScentGrid[_v.PosX[i], _v.PosY[i]] += _config.Scent.DepositAmount;
                    _v.FacingDirection[i] = 2;
                    break;

                case ZhiAction.MoveRight:
                    if (_v.PosX[i] < W - 1 && !_v.IsDeepWater(_v.PosX[i] + 1, _v.PosY[i]))
                        _v.PosX[i]++;
                    _v.ScentGrid[_v.PosX[i], _v.PosY[i]] += _config.Scent.DepositAmount;
                    _v.FacingDirection[i] = 3;
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

                case ZhiAction.Drink:
                    if (_v.IsAdjacentToWater(_v.PosX[i], _v.PosY[i])
                        || _v.IsShallowWater(_v.PosX[i], _v.PosY[i]))
                    {
                        float thirstBefore = _v.Thirst[i];
                        _v.Thirst[i] = MathF.Min(100f, _v.Thirst[i] + _config.Thirst.DrinkRestore);
                        float thirstDelta = _v.Thirst[i] - thirstBefore;
                        if (thirstDelta > 0) rewards[i] += thirstDelta * 0.05f;
                    }
                    else
                    {
                        rewards[i] -= 0.1f;
                    }
                    break;

                case ZhiAction.Hide:
                    if (!_v.IsHiding[i])
                    {
                        _v.IsHiding[i] = true;
                        _v.HideStartTick[i] = _globalTick;
                        _tickEvents.Add(new WorldEvent { Type = "hide_enter", AgentId = i, Tick = _globalTick });
                    }
                    break;
            }
        }

        // Resolve eat attempts (normal food: solo; BigFood: need multiple agents)
        ResolveEatAttempts(eatAttempts, rewards);
    }

    private void ResolveEatAttempts(List<int> eaters, float[] rewards)
    {
        var consumedFood = new HashSet<int>();
        var consumedCorpses = new HashSet<int>();
        int minAgents = _config.Grid.BigFoodMinAgents;

        // Group eaters by position
        var byPos = new Dictionary<(int x, int y), List<int>>();
        foreach (int i in eaters)
        {
            if (!_v.Alive[i]) continue;
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
            // Check food first (area-based for multi-cell BigFood), then corpse
            int foodIdx = -1;
            for (int f = 0; f < _v.FoodTiles.Count; f++)
            {
                if (consumedFood.Contains(f)) continue;
                var ft = _v.FoodTiles[f];
                int fw = ft.Width > 0 ? ft.Width : 1;
                int fh = ft.Height > 0 ? ft.Height : 1;
                if (pos.x >= ft.X && pos.x < ft.X + fw && pos.y >= ft.Y && pos.y < ft.Y + fh)
                { foodIdx = f; break; }
            }

            int corpseIdx = -1;
            if (foodIdx < 0)
            {
                for (int c = 0; c < _v.CorpseTiles.Count; c++)
                {
                    if (consumedCorpses.Contains(c)) continue;
                    if (_v.CorpseTiles[c].X == pos.x && _v.CorpseTiles[c].Y == pos.y)
                    { corpseIdx = c; break; }
                }
            }

            if (foodIdx < 0 && corpseIdx < 0)
            {
                // Failed eat: no HP penalty, just negative reward
                foreach (int i in agents)
                    rewards[i] -= 0.25f;
                continue;
            }

            if (foodIdx >= 0)
            {
                var tile = _v.FoodTiles[foodIdx];

                if (tile.IsBig)
                {
                    if (agents.Count >= minAgents)
                    {
                        // Cooperative eat: total = EatRestore * 2, shared equally
                        float totalRestore = _config.Hunger.EatRestore * 2f;
                        float share = totalRestore / agents.Count;
                        consumedFood.Add(foodIdx);
                        foreach (int i in agents)
                        {
                            float hungerBefore = _v.Hunger[i];
                            _v.Hunger[i] = MathF.Min(100f, _v.Hunger[i] + share);
                            float hungerDelta = _v.Hunger[i] - hungerBefore;
                            if (hungerDelta > 0) rewards[i] += hungerDelta * 0.05f;
                            _v.EatCount[i]++;
                            _v.BigFoodEatCount[i]++;
                            _tickEvents.Add(new WorldEvent { Type = "eat", AgentId = i, FoodType = "BigFood", Value = share, Tick = _globalTick });
                        }
                        _genBigFoodEaten++;
                        _genBigFoodEnergy += tile.Energy;
                    }
                    else
                    {
                        // Solo eat: 40% of EatRestore
                        float soloRestore = _config.Hunger.EatRestore * 0.4f;
                        consumedFood.Add(foodIdx);
                        foreach (int i in agents)
                        {
                            float hungerBefore = _v.Hunger[i];
                            _v.Hunger[i] = MathF.Min(100f, _v.Hunger[i] + soloRestore);
                            float hungerDelta = _v.Hunger[i] - hungerBefore;
                            if (hungerDelta > 0) rewards[i] += hungerDelta * 0.05f;
                            _v.EatCount[i]++;
                            _v.BigFoodEatCount[i]++;
                            _tickEvents.Add(new WorldEvent { Type = "eat", AgentId = i, FoodType = "BigFood", Value = soloRestore, Tick = _globalTick });
                        }
                        _genBigFoodEaten++;
                        _genBigFoodEnergy += soloRestore * agents.Count;
                    }
                }
                else
                {
                    // Normal food: first eater wins
                    consumedFood.Add(foodIdx);
                    int winner = agents[0];
                    float hungerBefore = _v.Hunger[winner];
                    _v.Hunger[winner] = MathF.Min(100f, _v.Hunger[winner] + _config.Hunger.EatRestore);
                    float hungerDelta = _v.Hunger[winner] - hungerBefore;
                    if (hungerDelta > 0) rewards[winner] += hungerDelta * 0.05f;
                    _v.EatCount[winner]++;
                    _v.FoodEatCount[winner]++;
                    _tickEvents.Add(new WorldEvent { Type = "eat", AgentId = winner, FoodType = "Food", Value = _config.Hunger.EatRestore, Tick = _globalTick });
                    _genFoodEaten++;
                    _genFoodEnergy += tile.Energy;

                    // Other eaters at same position fail
                    for (int k = 1; k < agents.Count; k++)
                        rewards[agents[k]] -= 0.25f;
                }
            }
            else
            {
                // Eating corpse: 80% of EatRestore
                var corpse = _v.CorpseTiles[corpseIdx];
                consumedCorpses.Add(corpseIdx);
                float corpseRestore = _config.Hunger.EatRestore * 0.8f;
                float share = corpseRestore / agents.Count;
                foreach (int i in agents)
                {
                    float hungerBefore = _v.Hunger[i];
                    _v.Hunger[i] = MathF.Min(100f, _v.Hunger[i] + share);
                    float hungerDelta = _v.Hunger[i] - hungerBefore;
                    if (hungerDelta > 0) rewards[i] += hungerDelta * 0.05f;
                    _v.EatCount[i]++;
                    _v.CorpseEatCount[i]++;
                    _tickEvents.Add(new WorldEvent { Type = "eat", AgentId = i, FoodType = "Corpse", Value = share, Tick = _globalTick });
                }
                _genCorpsesEaten++;
                _genCorpseEnergy += corpse.Energy;
            }
        }

        // Remove consumed items (reverse order)
        lock (_v.LockObj)
        {
            var sortedFood = consumedFood.OrderByDescending(x => x).ToList();
            foreach (int idx in sortedFood)
                _v.FoodTiles.RemoveAt(idx);

            var sortedCorpses = consumedCorpses.OrderByDescending(x => x).ToList();
            foreach (int idx in sortedCorpses)
                _v.CorpseTiles.RemoveAt(idx);
        }
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
            _tickEvents.Add(new WorldEvent
            {
                Type = "attack", AgentId = attacker, TargetId = bestTarget,
                Value = _config.Combat.StressPerAttack, Tick = _globalTick
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

        // Deposit onto spatial signal field (additive with cap)
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        if (sx >= 0 && sx < W && sy >= 0 && sy < H && signalValue >= 0 && signalValue < 4)
            _v.SignalField[sx, sy, signalValue] = MathF.Min(1.0f,
                _v.SignalField[sx, sy, signalValue] + 0.5f);
    }

    // PLACEHOLDER_FOOD

    private void GenerateRiver()
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        int riverWidth = _config.River.Width;
        int deepWidth = _config.River.DeepWidth;

        // Random river: starts from one edge, walks across map with slight randomness
        // Choose orientation: horizontal or vertical
        bool horizontal = _rng.Next(2) == 0;

        if (horizontal)
        {
            int yCenter = _rng.Next(H / 4, 3 * H / 4);
            for (int x = 0; x < W; x++)
            {
                // Slight random walk
                yCenter += _rng.Next(-1, 2);
                yCenter = Math.Clamp(yCenter, riverWidth, H - riverWidth - 1);

                for (int dy = -riverWidth / 2; dy <= riverWidth / 2; dy++)
                {
                    int y = yCenter + dy;
                    if (y < 0 || y >= H) continue;

                    int distFromCenter = Math.Abs(dy);
                    if (distFromCenter < deepWidth)
                        _v.RiverGrid[x, y] = 2; // deep
                    else
                        _v.RiverGrid[x, y] = 1; // shallow
                }
            }
        }
        else
        {
            int xCenter = _rng.Next(W / 4, 3 * W / 4);
            for (int y = 0; y < H; y++)
            {
                xCenter += _rng.Next(-1, 2);
                xCenter = Math.Clamp(xCenter, riverWidth, W - riverWidth - 1);

                for (int dx = -riverWidth / 2; dx <= riverWidth / 2; dx++)
                {
                    int x = xCenter + dx;
                    if (x < 0 || x >= W) continue;

                    int distFromCenter = Math.Abs(dx);
                    if (distFromCenter < deepWidth)
                        _v.RiverGrid[x, y] = 2; // deep
                    else
                        _v.RiverGrid[x, y] = 1; // shallow
                }
            }
        }

        Log($"[River] Generated {(horizontal ? "H" : "V")} river, width={riverWidth}, deep={deepWidth}");
    }

    private void ComputeWaterSound()
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        int range = _config.River.SoundRange;
        float decay = _config.River.SoundDecay;

        Array.Clear(_v.WaterSoundGrid);

        // For each water cell, propagate sound outward
        for (int wx = 0; wx < W; wx++)
        {
            for (int wy = 0; wy < H; wy++)
            {
                if (_v.RiverGrid[wx, wy] == 0) continue;

                // BFS from this water cell
                for (int dx = -range; dx <= range; dx++)
                {
                    for (int dy = -range; dy <= range; dy++)
                    {
                        int tx = wx + dx;
                        int ty = wy + dy;
                        if (tx < 0 || tx >= W || ty < 0 || ty >= H) continue;

                        int dist = Math.Abs(dx) + Math.Abs(dy); // Manhattan distance
                        if (dist > range) continue;

                        float sound = MathF.Pow(decay, dist);
                        _v.WaterSoundGrid[tx, ty] += sound;
                    }
                }
            }
        }
    }

    private void SpawnInitialFood()
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;

        // Spawn normal food
        for (int i = 0; i < _config.Grid.InitialFood; i++)
        {
            int x = _rng.Next(W);
            int y = _rng.Next(H);
            lock (_v.LockObj)
                _v.FoodTiles.Add(new FoodTile
                {
                    X = x, Y = y,
                    Width = 1, Height = 1,
                    TTL = _config.Grid.FoodTTL,
                    Energy = _config.Grid.FoodEnergy,
                    IsBig = false
                });
        }

        // Spawn BigFood (2x2 multi-cell)
        for (int i = 0; i < _config.Grid.InitialBigFood; i++)
        {
            bool placed = false;
            for (int attempt = 0; attempt < 50; attempt++)
            {
                int x = _rng.Next(W - 1);
                int y = _rng.Next(H - 1);

                // Check overlap with existing food
                bool overlap = false;
                for (int fx = 0; fx < 2 && !overlap; fx++)
                    for (int fy = 0; fy < 2 && !overlap; fy++)
                        if (_v.HasFoodAt(x + fx, y + fy, out _))
                            overlap = true;

                if (overlap) continue;

                lock (_v.LockObj)
                    _v.FoodTiles.Add(new FoodTile
                    {
                        X = x, Y = y,
                        Width = 2, Height = 2,
                        TTL = _config.Grid.BigFoodTTL,
                        Energy = _config.Grid.BigFoodEnergy,
                        EatTime = _config.Grid.BigFoodEatTime,
                        IsBig = true
                    });
                placed = true;
                break;
            }
            if (!placed)
                Log($"[Cosmos] WARNING: Could not place BigFood #{i} after 50 attempts");
        }
    }

    private int GetAliveCount()
    {
        int count = 0;
        for (int i = 0; i < _v.N; i++)
            if (_v.Alive[i]) count++;
        return count;
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
            if (nx >= 0 && nx < W && ny >= 0 && ny < H)
            {
                bool occupied = false;
                for (int a = 0; a < _v.N; a++)
                {
                    if (_v.Alive[a] && _v.PosX[a] == nx && _v.PosY[a] == ny)
                    { occupied = true; break; }
                }
                if (!occupied) { cx = nx; cy = ny; found = true; break; }
            }
        }

        if (!found) return -1; // No empty adjacent cell

        // Parent pays cost
        _v.Existence[parentIdx] -= _config.Reproduce.ParentCost;

        // Create child with mutated brain
        int childIdx = _v.AddAgent(cx, cy, _config.Reproduce.ChildStart);

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

    private void EndGeneration()
    {
        try
        {
            EndGenerationInternal();
        }
        catch (Exception ex)
        {
            Log($"[Cosmos] ERROR in EndGeneration: {ex.Message}");
            Console.WriteLine($"[Cosmos] ERROR in EndGeneration: {ex}");
            try
            {
                Log($"[Cosmos] Attempting generation recovery...");
                _generation++;
                InitializeGeneration(loadWeights: null);
                Log($"[Cosmos] ===== Gen {_generation} START (recovery) =====");
            }
            catch (Exception ex2)
            {
                Log($"[Cosmos] FATAL: Recovery failed: {ex2.Message}");
                Console.WriteLine($"[Cosmos] FATAL: Recovery failed: {ex2}");
                HardResetWorld();
            }
        }
    }

    private void EndGenerationInternal()
    {
        Log($"[Cosmos] ===== Gen {_generation} END (tick {_globalTick}, exceptions={_tickExceptionCount}) =====");
        _tickExceptionCount = 0;
        int actualN = _v.N;
        int configuredN = _config.Cosmos.AgentCount;

        _totalDeaths += actualN;
        _config.DeathCount = _totalDeaths;
        SaveConfig();

        for (int i = 0; i < actualN; i++)
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

        // Genetic selection — build exactly configuredN weights
        var sorted = _genResults.OrderByDescending(r => r.Fitness).ToList();
        var nextWeights = new List<byte[]>();
        int eliteCount = _config.Cosmos.EliteCount;

        for (int i = 0; i < configuredN; i++)
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
        Log($"[Cosmos] ===== Gen {_generation} START =====");
        InitializeGeneration(nextWeights);
    }

    private void HardResetWorld()
    {
        Log($"[Cosmos] HARD RESET — reinitializing world from scratch");
        Console.WriteLine($"[Cosmos] HARD RESET");
        try
        {
            _generation++;
            _tickExceptionCount = 0;
            _genResults.Clear();
            InitializeGeneration(loadWeights: null);
            Log($"[Cosmos] ===== Gen {_generation} START (hard reset) =====");
        }
        catch (Exception ex)
        {
            Log($"[Cosmos] FATAL: Hard reset failed: {ex.Message}");
            Console.WriteLine($"[Cosmos] FATAL: Hard reset failed: {ex}");
            throw;
        }
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

public class WorldEvent
{
    public string Type { get; set; } = "";
    public int AgentId { get; set; }
    public int TargetId { get; set; } = -1;
    public int ChildId { get; set; } = -1;
    public string FoodType { get; set; } = "";
    public int SignalValue { get; set; } = -1;
    public float Value { get; set; }
    public int Tick { get; set; }
}
