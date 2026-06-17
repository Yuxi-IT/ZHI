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
    public int Port { get; set; } = 19816;
    public int DecisionIntervalMs { get; set; } = 200;
    public int DeathCount { get; set; } = 0;
}

public sealed class ExistenceConfig
{
    public float Initial { get; set; } = 100.0f;
    public float DecayPerTick { get; set; } = 0.15f;
    public float EatBonus { get; set; } = 10.0f;
    public float EatFailPenalty { get; set; } = 1.0f;
}

public sealed class GridConfig
{
    public int Width { get; set; } = 64;
    public int Height { get; set; } = 64;
    public int MaxFood { get; set; } = 500;
    public float FoodSpawnChance { get; set; } = 0.4f;
    public int FoodTTL { get; set; } = 100;
    public float BigFoodChance { get; set; } = 0.02f;
    public float BigFoodBonus { get; set; } = 40f;
    public int BigFoodMinAgents { get; set; } = 2;
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
