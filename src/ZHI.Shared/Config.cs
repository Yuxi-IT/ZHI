namespace ZHI.Shared;

public sealed class ZhiConfig
{
    public ExistenceConfig Existence { get; set; } = new();
    public GridConfig Grid { get; set; } = new();
    public CombatConfig Combat { get; set; } = new();
    public SignalConfig Signal { get; set; } = new();
    public ScentConfig Scent { get; set; } = new();
    public FoodScentConfig FoodScent { get; set; } = new();
    public NetworkConfig Network { get; set; } = new();
    public CosmosConfig Cosmos { get; set; } = new();
    public CorpseConfig Corpse { get; set; } = new();
    public ReproduceConfig Reproduce { get; set; } = new();
    public AgeDeathConfig AgeDeath { get; set; } = new();
    public ThirstConfig Thirst { get; set; } = new();
    public HungerConfig Hunger { get; set; } = new();
    public RiverConfig River { get; set; } = new();
    public TemperatureConfig Temperature { get; set; } = new();
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
    public float FoodDecayPerTick { get; set; } = 0.075f;     // natural spoilage (~200 ticks at 15 energy)
    public float BigFoodDecayPerTick { get; set; } = 0.2f;     // natural spoilage (~400 ticks at 80 energy)

    // Eating: per-tick energy extraction (continuous model, multiple agents compete)
    public float FoodPerTickEnergy { get; set; } = 1.0f;     // ~15 ticks to deplete small food
    public float BigFoodPerTickEnergy { get; set; } = 2.0f;   // ~40 ticks solo, faster with competition
    public float CorpsePerTickEnergy { get; set; } = 1.0f;    // ~20 ticks to deplete corpse

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
    public float DiffusionRate { get; set; } = 0.1f;
}

public sealed class FoodScentConfig
{
    public float DecayRate { get; set; } = 0.85f;
    public float DiffusionRate { get; set; } = 0.08f;
    public float SmallFoodEmission { get; set; } = 0.3f;
    public float BigFoodEmission { get; set; } = 1.0f;
    public int SpreadRadius { get; set; } = 2;
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
    public int RespawnDelayTicks { get; set; } = 25;
}

public sealed class CorpseConfig
{
    public float Energy { get; set; } = 20f;
    public float DecayPerTick { get; set; } = 0.067f;  // natural decay (~300 ticks at 20 energy)
    public float ScentAmount { get; set; } = 0.5f;
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
    // hunger restored by per-tick food energy extraction
    public float PenaltyStart { get; set; } = 60f;       // continuous ramp begins here
    public float MaxPenalty { get; set; } = 0.25f;       // HP/tick at hunger=0
}

public sealed class RiverConfig
{
    public int Count { get; set; } = 1;          // number of rivers to generate
    public int Width { get; set; } = 5;          // total river corridor width
    public int DeepWidth { get; set; } = 1;       // deep water center width
    public int FordChance { get; set; } = 25;     // % chance per step to make deep→shallow (ford)
    public int SoundRange { get; set; } = 10;     // water sound propagation range
    public float SoundDecay { get; set; } = 0.9f; // per-cell sound decay
}

public sealed class TemperatureConfig
{
    public float MaxTemp { get; set; } = 35f;       // peak @ 14:00
    public float MinTemp { get; set; } = 5f;        // trough @ 04:00
    public float ColdThreshold { get; set; } = 15f;  // below this → cold HP decay
    public float MaxColdDecay { get; set; } = 0.15f; // extra HP/tick at coldest
    public float HotThreshold { get; set; } = 30f;   // above this → thirst accelerates
    public float MaxThirstAccel { get; set; } = 1.5f;// thirst multiplier at hottest
    public float HuddleRange { get; set; } = 2f;     // Manhattan distance for warmth sharing
    public float HuddleWarmthPerAgent { get; set; } = 3f; // effective °C per nearby agent
}


