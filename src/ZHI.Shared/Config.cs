namespace ZHI.Shared;

public sealed class ZhiConfig
{
    public ExistenceConfig Existence { get; set; } = new();
    public GridConfig Grid { get; set; } = new();
    public CombatConfig Combat { get; set; } = new();
    public ChemicalConfig Chemical { get; set; } = new();
    public GenomeConfig Genome { get; set; } = new();
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
    public StaminaConfig Stamina { get; set; } = new();
    public NutrientConfig Nutrient { get; set; } = new();
    public WaterCycleConfig WaterCycle { get; set; } = new();
    public PlantConfig Plant { get; set; } = new();
    public int Port { get; set; } = 19816;
    public int DecisionIntervalMs { get; set; } = 200;
    public int DeathCount { get; set; } = 0;
    public int? Seed { get; set; }
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

    // Initial plant spawn
    public int InitialFood { get; set; } = 50;
    public float FoodEnergy { get; set; } = 10f;
    public float FoodDecayPerTick { get; set; } = 0.05f;      // natural plant decay

    // Eating: per-tick energy extraction (continuous model, multiple agents compete)
    public float FoodPerTickEnergy { get; set; } = 1.0f;
    public float CorpsePerTickEnergy { get; set; } = 1.0f;    // ~20 ticks to deplete corpse
}

public sealed class CombatConfig
{
    public float StressPerAttack { get; set; } = 0.5f;
    public float StressDamage { get; set; } = 0.1f;
    public float StressDecay { get; set; } = 0.1f;
    public int AttackRange { get; set; } = 1;
    public float AttackCost { get; set; } = 1.0f;
}

public sealed class ChemicalConfig
{
    public float EmissionCost { get; set; } = 3f;  // stamina cost per emission
    public float DiffusionRate { get; set; } = 0.1f; // per-tick spread to neighbors
    public float DecayRate { get; set; } = 0.95f;    // per-tick multiplicative decay
    public int WaveRadius { get; set; } = 4;         // Chebyshev radius of emission
}

public sealed class GenomeConfig
{
    public float MutationStd { get; set; } = 0.05f;  // gaussian noise per trait per generation
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
    public float AgentBodyHeat { get; set; } = 2f;   // °C per agent on own cell (half on 8 neighbors), scaled by HP ratio
    [Obsolete("Replaced by thermal diffusion + distance field in v4.3")]
    public float RiverCooling { get; set; } = 5f;
    [Obsolete("Replaced by thermal diffusion + distance field in v4.3")]
    public int RiverCoolingRange { get; set; } = 2;
    public float LandLerpRate { get; set; } = 0.25f;     // land temp convergence rate to ambient per tick
    public float WaterHeatCapacity { get; set; } = 4.0f;  // water temp change multiplier (higher = slower)
    public float ThermalDiffusionRate { get; set; } = 0.12f; // heat spread rate to 4 neighbors per tick
    public int RiverLandInfluence { get; set; } = 8;      // max Chebyshev distance river affects land lerp rate
    public float HypothermiaThreshold { get; set; } = 33f;   // below this body temp → HP decay
    public float HypothermiaMaxDamage { get; set; } = 0.08f; // max HP/tick at body temp 26°C
    public float WaterCoolingMult { get; set; } = 2f;       // body temp lerp multiplier in water
    public float DeepWaterExtraCold { get; set; } = 3f;     // extra °C penalty in deep water cells
    public float MinBodyTemp { get; set; } = 26f;           // body temp hard floor
}

public sealed class PlantConfig
{
    public float BaseGrowthRate { get; set; } = 0.05f;
    public float MaxPlantEnergy { get; set; } = 20f;
    public float SpreadChance { get; set; } = 0.02f;
    public int SpreadRadius { get; set; } = 2;
    public float MinTemp { get; set; } = 0f;
    public float OptimalTemp { get; set; } = 25f;
    public float MaxTemp { get; set; } = 45f;
    public float DeathTemp { get; set; } = -2f;
    public float WaterNeed { get; set; } = 0.2f;
    public float NutrientNeed { get; set; } = 0.5f;
    public float NutrientConsumption { get; set; } = 0.1f;
    public float WaterConsumption { get; set; } = 0.05f;
    public int InitialPlants { get; set; } = 50;
    public float InitialPlantEnergy { get; set; } = 10f;
}

public sealed class WaterCycleConfig
{
    public float SurfaceWaterMaxDepth { get; set; } = 3f;
    public float SurfaceFlowRate { get; set; } = 0.3f;
    public float EvaporationRate { get; set; } = 0.05f;
    public float MaxGroundwater { get; set; } = 1f;
    public float AbsorptionRate { get; set; } = 0.1f;
    public float GroundwaterDiffusionRate { get; set; } = 0.01f;
    public float RainAmount { get; set; } = 0.5f;
    public int RainIntervalMin { get; set; } = 200;
    public int RainIntervalMax { get; set; } = 600;
    public int RainRadius { get; set; } = 10;
}

public sealed class NutrientConfig
{
    public float CorpseToNutrientRatio { get; set; } = 0.5f;
    public float PlantToNutrientRatio { get; set; } = 0.3f;
    public float DiffusionRate { get; set; } = 0.02f;
    public float MaxNutrient { get; set; } = 10f;
    public float InitialNutrient { get; set; } = 2f;
}

public sealed class StaminaConfig
{
    public float MaxStamina { get; set; } = 100f;
    public float MoveCost { get; set; } = 0.5f;
    public float AttackCost { get; set; } = 8f;
    public float ChemicalEmitCost { get; set; } = 3f;
    public float ShallowWaterMoveExtra { get; set; } = 1f;
    public float DeepWaterMoveExtra { get; set; } = 2.5f;
    public float DeepWaterClimbExtra { get; set; } = 1f;
    public float BaseRecovery { get; set; } = 1f;         // per tick when well-fed
    public float StationaryRecoveryBonus { get; set; } = 2f; // multiplier when stationary
    public float LowStaminaThreshold { get; set; } = 10f;   // cannot push/terraform below this
    public int StationaryTicksRequired { get; set; } = 5;   // ticks without movement
    public float StationaryDamageMult { get; set; } = 1.2f; // 120% damage taken when stationary
    public float StationarySelfHeat { get; set; } = 3f;     // own cell heat bonus
    public float StationaryNeighborHeat { get; set; } = 2f; // neighbor cell heat bonus
    public float StationaryHpRecoveryBonus { get; set; } = 0.1f; // bonus HP/tick when stationary
}


