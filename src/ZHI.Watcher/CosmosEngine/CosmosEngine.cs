using System.Text.Json;
using ZHI.Core;
using ZHI.Shared;
using TorchSharp;
using static TorchSharp.torch;

namespace ZHI.Watcher;

/// <summary>
/// 空间化网格生态系统引擎 — 128×128 grid, 7 actions, Stress combat, Scent trails
/// </summary>
public partial class CosmosEngine : IDisposable
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
    private ZhiConfig? _pendingConfig;
    private volatile bool _pendingRestart;
    private float _effectiveMutationRate = 0.1f;
    private float _totalEnergyInWorld;
    private long _totalWorldTicks; // cumulative ticks, never resets
    private int[] _deathTick = Array.Empty<int>();
    private int[] _lastAttackTick = Array.Empty<int>();
    private int _respawnCount;
    private float _gameTimeOfDay;
    private float _temperature;

    // Per-tick reusable buffers (avoid GC allocations)
    private float[] _rewardBuf = Array.Empty<float>();
    private float[] _donesBuf = Array.Empty<float>();
    private long[] _actionsBuf = Array.Empty<long>();
    private long[] _signalBuf = Array.Empty<long>();
    private float[] _logProbsBuf = Array.Empty<float>();
    private float[] _valuesBuf = Array.Empty<float>();
    private float[] _aliveMaskBuf = Array.Empty<float>();
    private float[] _intrinsicBuf = Array.Empty<float>();
    private float[] _scentBuf = Array.Empty<float>();
    private float[] _foodScentBuf = Array.Empty<float>();
    private float[] _stateForPpoBuf = Array.Empty<float>();
    private readonly HashSet<int> _depletedFoodSet = new();
    private readonly HashSet<int> _depletedCorpsesSet = new();

    // Event broadcasting
    private readonly List<WorldEvent> _tickEvents = new();
    public IReadOnlyList<WorldEvent> TickEvents => _tickEvents;

    // Generation statistics
    private int _genAttacks;
    private int _genFoodEaten;
    private int _genBigFoodEaten;
    private int _genCorpsesEaten;
    private int _genTotalTicks;
    private float _genFoodEnergy;
    private float _genBigFoodEnergy;
    private float _genCorpseEnergy;

    public int GenAttacks => _genAttacks;
    public int GenFoodEaten => _genFoodEaten;
    public int GenBigFoodEaten => _genBigFoodEaten;
    public int GenCorpsesEaten => _genCorpsesEaten;
    public int GenTotalTicks => _genTotalTicks;
    public float GenFoodEnergy => _genFoodEnergy;
    public float GenBigFoodEnergy => _genBigFoodEnergy;
    public float GenCorpseEnergy => _genCorpseEnergy;
    public ZhiConfig CurrentConfig => _config;

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
    public float GameTimeOfDay => _gameTimeOfDay;
    public float Temperature => _temperature;
    public long WorldDay => _totalWorldTicks / 3600 + 1;

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

        _deathTick = new int[n];
        Array.Fill(_deathTick, -1);
        _lastAttackTick = new int[n];

        _generation = config.DeathCount + 1;
        _totalDeaths = config.DeathCount;

        Log($"[Cosmos] Grid world init: {n} agents, gen {_generation}, device {ZHI.Core.Device.Name}");
    }

    public async Task RunAsync()
    {
        try
        {
            // Auto-resume from latest generation if available
            List<byte[]>? resumeWeights = null;
            var latestGen = _blackbox.GetLatestGeneration();
            if (latestGen != null)
            {
                _generation = latestGen.Generation + 1;
                var bestWeights = _blackbox.LoadWeights();
                if (bestWeights != null)
                {
                    resumeWeights = new List<byte[]> { bestWeights };
                    Log($"[Cosmos] Resuming from Gen {latestGen.Generation}, starting Gen {_generation}");
                }
            }
            InitializeGeneration(resumeWeights);

            while (!_cts.Token.IsCancellationRequested)
            {
                if (_pendingRestart)
                {
                    _pendingRestart = false;
                    Log("[Cosmos] Reinitializing world with new config...");
                    int bestIdx = -1; int bestTicks = 0;
                    for (int j = 0; j < _v.N; j++)
                        if (_v.Alive[j] && _v.TickCount[j] > bestTicks) { bestIdx = j; bestTicks = _v.TickCount[j]; }
                    if (bestIdx >= 0 && bestIdx < _agentWeights.Count)
                        _blackbox.SaveWeights(_generation, _agentWeights[bestIdx]);
                    var restartWeights = new List<byte[]> { _agentWeights[bestIdx >= 0 ? bestIdx : 0] };
                    InitializeGeneration(restartWeights);
                    _generation++;
                    Log($"[Cosmos] World restarted as Gen {_generation}");
                    continue;
                }

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

    private void Tick()
    {
        _globalTick++;
        _totalWorldTicks++;
        int n = _v.N;
        _tickEvents.Clear();
        _genTotalTicks++;

        ApplyWorldTemperature(n);

        ApplyAgentPhysiology(n);

        ApplyScentPhysics();

        ApplyFoodDecayAndRespawn();

        // 6. GRU inference (uses StateMatrix built at end of previous tick)
        var (actions, signalValues, logProbs, values, entropy, newHidden) =
            _gruBrain.StepForward(_v.StateMatrix, _gruHidden);
        _gruHidden?.Dispose();
        _gruHidden = newHidden;

        // NaN guard: if entropy is NaN, the policy has diverged — reset hidden state
        if (entropy.isnan().any().item<bool>())
        {
            Log("[Cosmos] NaN detected in policy — resetting hidden state");
            _gruHidden?.Dispose();
            _gruHidden = torch.zeros(1, n, _gruBrain.HiddenSize, device: _v.Device);
        }

        // Extract to CPU
        using (var cpuActs = actions.cpu()) cpuActs.data<long>().CopyTo(_actionsBuf);
        using (var cpuSigs = signalValues.cpu()) cpuSigs.data<long>().CopyTo(_signalBuf);
        using (var cpuLp = logProbs.cpu()) cpuLp.data<float>().CopyTo(_logProbsBuf);
        using (var cpuVals = values.cpu()) cpuVals.data<float>().CopyTo(_valuesBuf);

        // 8. Process actions
        Array.Clear(_rewardBuf, 0, n);
        ProcessActions(_actionsBuf, _signalBuf, _rewardBuf);

        // 8b. Stationary detection + Stamina recovery
        for (int i = 0; i < n; i++)
        {
            if (!_v.Alive[i]) continue;

            // Stationary判定: Attack also resets counter
            long act = _actionsBuf[i];
            bool isMovingOrFighting = act >= 0 && act <= 3 || act == 5 || act >= 8;
            if (isMovingOrFighting)
                _v.TicksSinceLastMove[i] = 0;
            else
                _v.TicksSinceLastMove[i]++;

            _v.IsStationary[i] = _v.TicksSinceLastMove[i] >= _config.Stamina.StationaryTicksRequired;

            // Stamina recovery: well-fed + hydrated + healthy
            if (_v.Hunger[i] > 60f && _v.Thirst[i] > 70f)
            {
                float recovery = _config.Stamina.BaseRecovery;
                if (_v.IsStationary[i]) recovery *= _config.Stamina.StationaryRecoveryBonus;
                _v.Stamina[i] = Math.Clamp(_v.Stamina[i] + recovery, 0f, _config.Stamina.MaxStamina);
            }

            // Stationary HP recovery bonus
            if (_v.IsStationary[i])
            {
                _v.Existence[i] = MathF.Min(_v.Existence[i] + _config.Stamina.StationaryHpRecoveryBonus,
                    _config.Existence.Initial);
            }
        }

        // 8c. Signal field decay (after all deposits this tick)
        for (int x = 0; x < ToolDefinitions.GridWidth; x++)
            for (int y = 0; y < ToolDefinitions.GridHeight; y++)
                for (int ch = 0; ch < 4; ch++)
                    _v.SignalField[x, y, ch] *= 0.9f;

        // 9. Death check + corpse spawning
        Array.Clear(_donesBuf, 0, n);
        for (int i = 0; i < n; i++)
        {
            if (!_v.Alive[i]) continue;
            if (_v.Existence[i] <= 0f)
            {
                _v.Alive[i] = false;
                _v.StatusMirror[i] = "DEAD";
                _rewardBuf[i] = -20f;
                _donesBuf[i] = 1f;
                _deathTick[i] = _globalTick;

                _tickEvents.Add(new WorldEvent { Type = "death", AgentId = i, Tick = _globalTick });

                // Spawn corpse at death position
                lock (_v.LockObj)
                    _v.CorpseTiles.Add(new CorpseTile
                    {
                        X = _v.PosX[i],
                        Y = _v.PosY[i],
                        Energy = _config.Corpse.Energy
                    });
            }
        }

        // Survival reward for alive agents
        for (int i = 0; i < n; i++)
        {
                if (_v.Alive[i])
                _rewardBuf[i] += 0.1f;
        }

        // RND intrinsic curiosity
        using (var scope = torch.NewDisposeScope())
        {
            using var intrinsic = _rnd!.ComputeIntrinsicReward(_v.StateMatrix);
            using (var cpuIntr = intrinsic.cpu()) cpuIntr.data<float>().CopyTo(_intrinsicBuf);
            for (int i = 0; i < n; i++)
                if (_v.Alive[i]) _rewardBuf[i] += _intrinsicBuf[i];
        }

        // 10. Update tracking
        for (int i = 0; i < n; i++)
        {
            if (_v.Alive[i] || _donesBuf[i] == 1f)
            {
                _v.LastAction[i] = _actionsBuf[i];
                _v.LastActionNameMirror[i] = ToolDefinitions.ActionNames[(int)_actionsBuf[i]];
                _v.TickCount[i]++;
            }
        }

        // Reset GRU hidden for dead agents
        using (var dScope = torch.NewDisposeScope())
        {
            for (int i = 0; i < n; i++) _aliveMaskBuf[i] = _v.Alive[i] ? 1f : 0f;
            using var mask = tensor(_aliveMaskBuf, device: _v.Device).unsqueeze(0).unsqueeze(-1);
            _gruHidden!.mul_(mask);
        }

        // 10b. Terrain physics: weathering, flooding, evaporation
        SettleTerrainPhysics();

        // 11. Snapshot current observation (s_t) for PPO storage
        int stateSize = ToolDefinitions.StateSize;
        int nn = _v.N;
        if (_stateForPpoBuf.Length < nn * stateSize) _stateForPpoBuf = new float[nn * stateSize];
        Array.Copy(_v.GetStateBuffer(), _stateForPpoBuf, nn * stateSize);

        // 11b. Rebuild spatial grids + build next observation (s_{t+1}) for GRU
        _v.RebuildSpatialGrids();
        _v.BuildStateMatrix();

        // 11c. Store in PPO buffer (s_t, a_t, r_t, v_t)
        _ppoBuffer!.Store(_stateForPpoBuf, _actionsBuf, _signalBuf, _logProbsBuf, _rewardBuf, _donesBuf, _valuesBuf);

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

            // Reset all GRU hidden states after policy update to prevent stale temporal context
            using (var _ = torch.no_grad())
                _gruHidden!.zero_();
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
                && _v.N < _config.Cosmos.AgentCount
                && (_globalTick - _v.LastReproduceTick[i]) >= _config.Reproduce.Cooldown)
            {
                // Population pressure: reduce reproduction chance when overcrowded
                if (aliveNow > 128 && _rng.NextDouble() > 0.2) continue;
                int childIdx = ReproduceAgent(i);
                if (childIdx >= 0)
                {
                    aliveNow++;
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
            ResizeTickBuffers(n);

            // Grow death tick array
            var oldDeathTick = _deathTick;
            _deathTick = new int[n];
            Array.Fill(_deathTick, -1);
            Array.Copy(oldDeathTick, _deathTick, oldDeathTick.Length);

            // Grow attack tick array
            var oldAttackTick = _lastAttackTick;
            _lastAttackTick = new int[n];
            Array.Copy(oldAttackTick, _lastAttackTick, oldAttackTick.Length);
        }

        // 14. Individual respawn: dead agents respawn after RespawnDelayTicks
        RespawnDeadAgents();

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
