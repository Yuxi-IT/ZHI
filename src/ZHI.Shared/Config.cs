namespace ZHI.Shared;

public sealed class ZhiConfig
{
    public ExistenceConfig Existence { get; set; } = new();
    public GridConfig Grid { get; set; } = new();
    public CombatConfig Combat { get; set; } = new();
    public SignalConfig Signal { get; set; } = new();
    public ScentConfig Scent { get; set; } = new();
    public NetworkConfig Network { get; set; } = new();
    public CosmosConfig Cosmos { get; set; } = new();
    public CorpseConfig Corpse { get; set; } = new();
    public ReproduceConfig Reproduce { get; set; } = new();
    public HideConfig Hide { get; set; } = new();
    public AgeDeathConfig AgeDeath { get; set; } = new();
    public ThirstConfig Thirst { get; set; } = new();
    public HungerConfig Hunger { get; set; } = new();
    public RiverConfig River { get; set; } = new();
    public BushConfig Bush { get; set; } = new();
    public int Port { get; set; } = 19816;
    public int DecisionIntervalMs { get; set; } = 200;
    public int DeathCount { get; set; } = 0;
}

public sealed class ExistenceConfig
{
    public float Initial { get; set; } = 100.0f;
    public float DecayPerTick { get; set; } = 0.1f;
}

public sealed class GridConfig
{
    public int Width { get; set; } = 64;
    public int Height { get; set; } = 64;
    public int MaxAgents { get; set; } = 512;

    // Initial food spawn
    public int InitialFood { get; set; } = 50;
    public int InitialBigFood { get; set; } = 10;
    public float FoodEnergy { get; set; } = 15f;
    public float BigFoodEnergy { get; set; } = 80f;
    public int FoodTTL { get; set; } = 200;
    public int BigFoodTTL { get; set; } = 400;

    // BigFood cooperative eating
    public int BigFoodEatTime { get; set; } = 5;
    public int BigFoodMinAgents { get; set; } = 2;

    // Food respawning
    public int FoodRespawnInterval { get; set; } = 10;
    public int MaxFood { get; set; } = 100;
}

public sealed class CombatConfig
{
    public float StressPerAttack { get; set; } = 0.5f;
    public float StressDamage { get; set; } = 0.1f;
    public float StressDecay { get; set; } = 0.1f;
    public int AttackRange { get; set; } = 1;
    public float AttackCost { get; set; } = 1.0f;
}

public sealed class SignalConfig
{
    public float Cost { get; set; } = 0.25f;
    public int NumValues { get; set; } = 4;
}

public sealed class ScentConfig
{
    public float DepositAmount { get; set; } = 1.0f;
    public float DecayRate { get; set; } = 0.95f;
}

public sealed class NetworkConfig
{
    public int[] HiddenSizes { get; set; } = [128, 128, 64];
    public float LearningRate { get; set; } = 0.001f;
    public float Gamma { get; set; } = 0.99f;
}

public sealed class CosmosConfig
{
    public int AgentCount { get; set; } = 64;
    public int EliteCount { get; set; } = 2;
    public float MutationRate { get; set; } = 0.1f;
    public float MutationStd { get; set; } = 0.02f;
    public float MutationRateMin { get; set; } = 0.02f;
    public int MutationDecayGenerations { get; set; } = 100;
}

public sealed class CorpseConfig
{
    public float Energy { get; set; } = 20f;
    public int TTL { get; set; } = 300;
    public float ScentAmount { get; set; } = 0.5f;
}

public sealed class HideConfig
{
    public float DecayMultiplier { get; set; } = 0.9f;
    public int MinDuration { get; set; } = 1;
    public int DetectionRange { get; set; } = 2;
    public float MetabolicMultiplier { get; set; } = 1.6f;
    public int MaxDurationTicks { get; set; } = 150;
    public float BreakStressPenalty { get; set; } = 1.5f;
}

public sealed class AgeDeathConfig
{
    public int Stage1Age { get; set; } = 5000;
    public float Stage1Decay { get; set; } = 0.02f;
    public int Stage2Age { get; set; } = 6000;
    public float Stage2Decay { get; set; } = 0.05f;
    public int Stage3Age { get; set; } = 7000;
    public float Stage3Decay { get; set; } = 0.1f;
    public int MaxAge { get; set; } = 8000;
}

public sealed class ReproduceConfig
{
    public float MinExistence { get; set; } = 80f;
    public int MinAge { get; set; } = 200;
    public float ParentCost { get; set; } = 40f;
    public float ChildStart { get; set; } = 40f;
    public float MutationScale { get; set; } = 0.3f;
    public int Cooldown { get; set; } = 500;
}

public sealed class ThirstConfig
{
    public float Initial { get; set; } = 100f;
    public float DecayRate { get; set; } = 0.1f;         // 1000 ticks to empty
    public float DrinkRestore { get; set; } = 40f;       // per drink action
    public float PenaltyStart { get; set; } = 70f;       // continuous ramp begins here
    public float MaxPenalty { get; set; } = 0.8f;        // HP/tick at thirst=0
}

public sealed class HungerConfig
{
    public float Initial { get; set; } = 100f;
    public float DecayRate { get; set; } = 0.05f;       // 2000 ticks to empty
    public float EatRestore { get; set; } = 30f;         // per eat action
    public float PenaltyStart { get; set; } = 60f;       // continuous ramp begins here
    public float MaxPenalty { get; set; } = 0.25f;       // HP/tick at hunger=0
}

public sealed class RiverConfig
{
    public int Width { get; set; } = 3;          // shallow water width
    public int DeepWidth { get; set; } = 1;       // deep water center width
    public int SoundRange { get; set; } = 10;     // water sound propagation range
    public float SoundDecay { get; set; } = 0.9f; // per-cell sound decay
}

public sealed class BushConfig
{
    public int ClusterCount { get; set; } = 15;       // number of bush clusters
    public int MinClusterSize { get; set; } = 10;     // min tiles per cluster
    public int MaxClusterSize { get; set; } = 30;     // max tiles per cluster
    public float OpenHideMetabolicMult { get; set; } = 2.5f;  // metabolic penalty in open
    public float OpenHideRewardPenalty { get; set; } = 0.2f;  // reward penalty in open
    public float BushHideReward { get; set; } = 0.1f;         // small reward for valid hide
}
