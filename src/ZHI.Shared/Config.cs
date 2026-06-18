namespace ZHI.Shared;

public sealed class ZhiConfig
{
    public GridConfig Grid { get; set; } = new();
    public CombatConfig Combat { get; set; } = new();
    [System.Text.Json.Serialization.JsonPropertyName("signal")]
    public ChemicalConfig Chemical { get; set; } = new();
    public GenomeConfig Genome { get; set; } = new();
    public ScentConfig Scent { get; set; } = new();
    public FoodScentConfig FoodScent { get; set; } = new();
    public NetworkConfig Network { get; set; } = new();
    public CosmosConfig Cosmos { get; set; } = new();
    public CorpseConfig Corpse { get; set; } = new();
    public ReproduceConfig Reproduce { get; set; } = new();
    public AgeDeathConfig AgeDeath { get; set; } = new();
    public MetabolismConfig Metabolism { get; set; } = new();
    public RiverConfig River { get; set; } = new();
    public TemperatureConfig Temperature { get; set; } = new();
    public NutrientConfig Nutrient { get; set; } = new();
    public WaterCycleConfig WaterCycle { get; set; } = new();
    public PlantConfig Plant { get; set; } = new();
    public WindConfig Wind { get; set; } = new();
    public SunlightConfig Sunlight { get; set; } = new();
    public BiomeConfig Biome { get; set; } = new();
    public int Port { get; set; } = 19816;
    public int DecisionIntervalMs { get; set; } = 200;
    public int DeathCount { get; set; } = 0;
    public int? Seed { get; set; }
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
}

public sealed class ChemicalConfig
{
    [System.Text.Json.Serialization.JsonPropertyName("cost")]
    public float EmissionCost { get; set; } = 3f;  // stamina cost per emission
    public float DiffusionRate { get; set; } = 0.1f; // per-tick spread to neighbors
    public float DecayRate { get; set; } = 0.95f;    // per-tick multiplicative decay
    [System.Text.Json.Serialization.JsonPropertyName("wave_radius")]
    public int WaveRadius { get; set; } = 4;         // Chebyshev radius of emission
    [System.Text.Json.Serialization.JsonPropertyName("num_values")]
    public int NumValues { get; set; } = 4;
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
    public float ScentAmount { get; set; } = 0.5f;

    // Temperature-driven decay
    public float DecayTempBase { get; set; } = 15f;    // baseline temp in C
    public float DecayTempRate { get; set; } = 0.003f; // decay acceleration per C above base
    public float DecayTempMin { get; set; } = 0.01f;   // minimum decay rate (cold doesn't stop)
    public float DecayTempMax { get; set; } = 0.2f;    // maximum decay rate

    // Humidity-driven decay
    public float DecayHumidityMult { get; set; } = 0.5f; // humidity multiplier on decay

    // Large corpse nutrient boost
    public float LargeCorpseThreshold { get; set; } = 50f; // Energy > this = large corpse
    public float LargeCorpseNutrientBoost { get; set; } = 2f; // nutrient release multiplier
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
    [System.Text.Json.Serialization.JsonPropertyName("min_energy")]
    public float MinEnergy { get; set; } = 80f;
    public int MinAge { get; set; } = 200;
    public float ParentCost { get; set; } = 40f;
    public float ChildStart { get; set; } = 40f;
    public float MutationScale { get; set; } = 0.3f;
    public int Cooldown { get; set; } = 500;
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
    public float ColdThreshold { get; set; } = 15f;  // below this body temp → cold metabolism extra decay
    public float MaxColdDecay { get; set; } = 0.08f; // extra Energy/tick at coldest
    public float HotThreshold { get; set; } = 30f;   // above this body temp → water decay accelerates
    [System.Text.Json.Serialization.JsonPropertyName("max_water_decay_mult")]
    public float MaxWaterDecayMult { get; set; } = 1.5f; // water decay multiplier at hottest
    public float HuddleRange { get; set; } = 2f;     // Manhattan distance for warmth sharing
    public float HuddleWarmthPerAgent { get; set; } = 3f; // effective °C per nearby agent
    public float AgentBodyHeat { get; set; } = 0.3f; // °C per agent on own cell (half on 8 neighbors), scaled by HP ratio
    [Obsolete("Replaced by thermal diffusion + distance field in v4.3")]
    public float RiverCooling { get; set; } = 5f;
    [Obsolete("Replaced by thermal diffusion + distance field in v4.3")]
    public int RiverCoolingRange { get; set; } = 2;
    public float LandLerpRate { get; set; } = 0.2f;      // land temp convergence rate to ambient per tick
    public float WaterHeatCapacity { get; set; } = 4.0f;  // water temp change multiplier (higher = slower)
    public float ThermalDiffusionRate { get; set; } = 0.12f; // heat spread rate to 4 neighbors per tick
    public int RiverLandInfluence { get; set; } = 8;      // max Chebyshev distance river affects land lerp rate
    public float HypothermiaThreshold { get; set; } = 18f;   // below this body temp → Energy damage
    public float HypothermiaMaxDamage { get; set; } = 0.03f; // max Energy/tick at body temp floor
    public float WaterCoolingMult { get; set; } = 1.5f;      // body temp lerp multiplier in water
    public float DeepWaterExtraCold { get; set; } = 2f;      // extra °C penalty in deep water cells
    public float WaterCoolingOffset { get; set; } = 6f;     // water cells target below ambient air
    public float HeightLapseRate { get; set; } = 0.04f;     // °C per height unit (height 0..255, centered at 128 → ±5°C)
    public float MinBodyTemp { get; set; } = 12f;           // body temp hard floor
}

public sealed class PlantSpeciesParams
{
    public float GrowthRate { get; set; } = 0.05f;
    public float MaxEnergy { get; set; } = 20f;
    public float NutrientNeed { get; set; } = 0.5f;
    public float WaterNeed { get; set; } = 0.2f;
    public int MaxAge { get; set; } = 4000;         // ticks before forced decay
    public int SeedDistance { get; set; } = 2;       // seed spread radius
    public float TempOptimal { get; set; } = 25f;    // optimal temperature in C
    public float TempRange { get; set; } = 22f;      // +/- tolerance around optimal
    public float DroughtResist { get; set; } = 0.5f; // 0-1, higher = less water stress
    public float MinTemp { get; set; } = 0f;         // absolute min (below = damage)
    public float MaxTemp { get; set; } = 45f;        // absolute max (above = damage)
    public float DeathTemp { get; set; } = -2f;      // instant death below this
    public bool EdibleAsSprout { get; set; } = true;  // can agents eat sprouts?
    public bool EdibleOnlyAdult { get; set; } = false; // only adult+decay are edible
}

public sealed class PlantSpeciesConfig
{
    public PlantSpeciesParams Grass { get; set; } = new()
    {
        GrowthRate = 0.12f, MaxEnergy = 8f,
        NutrientNeed = 0.3f, WaterNeed = 0.1f,
        MaxAge = 1500, SeedDistance = 3,
        TempOptimal = 22f, TempRange = 22f,
        DroughtResist = 0.2f,
        MinTemp = 0f, MaxTemp = 42f, DeathTemp = -2f,
        EdibleAsSprout = true, EdibleOnlyAdult = false,
    };

    public PlantSpeciesParams Bush { get; set; } = new()
    {
        GrowthRate = 0.06f, MaxEnergy = 25f,
        NutrientNeed = 0.6f, WaterNeed = 0.3f,
        MaxAge = 3000, SeedDistance = 2,
        TempOptimal = 25f, TempRange = 20f,
        DroughtResist = 0.6f,
        MinTemp = 2f, MaxTemp = 44f, DeathTemp = -1f,
        EdibleAsSprout = true, EdibleOnlyAdult = false,
    };

    public PlantSpeciesParams Tree { get; set; } = new()
    {
        GrowthRate = 0.03f, MaxEnergy = 60f,
        NutrientNeed = 1.0f, WaterNeed = 0.5f,
        MaxAge = 8000, SeedDistance = 5,
        TempOptimal = 20f, TempRange = 18f,
        DroughtResist = 0.4f,
        MinTemp = 3f, MaxTemp = 40f, DeathTemp = 0f,
        EdibleAsSprout = false, EdibleOnlyAdult = true,
    };
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
    public int InitialPlants { get; set; } = 60;
    public float InitialPlantEnergy { get; set; } = 10f;

    // Lifecycle stage thresholds (shared across species)
    public float SeedGermNutrientMin { get; set; } = 0.3f;
    public float SeedGermWaterMin { get; set; } = 0.2f;
    public int SeedGermDelay { get; set; } = 15;
    public float SproutAdultEnergy { get; set; } = 5f;
    public float SproutGrowthMult { get; set; } = 1.8f;
    public float SproutHealthLoss { get; set; } = 0.02f;
    public float DecayRate { get; set; } = 0.03f;
    public float DecayNutrientReturn { get; set; } = 0.6f;
    public float SeedInitialEnergy { get; set; } = 1f;
    public float SproutInitialHealth { get; set; } = 0.8f;
    public float AdultSeedCost { get; set; } = 2f;
    public int MaxPlants { get; set; } = 2000;

    public float VisibilityBlockMax { get; set; } = 0.8f;    // max occlusion per cell
    public int VisibilityBlockDistance { get; set; } = 3;     // Chebyshev distance affected by occlusion

    public PlantSpeciesConfig Species { get; set; } = new();
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
    public float SlopeRunoffMult { get; set; } = 2.0f;  // surface flow *= 1 + slope * k on steep terrain
    public float RiverDrainRate { get; set; } = 0.3f;   // fraction of surface water drained by adjacent river per tick
    public float PermeabilityBase { get; set; } = 1.0f;  // baseline soil permeability multiplier for infiltration
}

public sealed class NutrientConfig
{
    public float CorpseToNutrientRatio { get; set; } = 0.5f;
    public float PlantToNutrientRatio { get; set; } = 0.3f;
    public float DiffusionRate { get; set; } = 0.02f;
    public float MaxNutrient { get; set; } = 10f;
    public float InitialNutrient { get; set; } = 2f;
    public float RiverBankNutrientBoost { get; set; } = 3f;   // peak extra nutrient at riverbank
    public int RiverBankDistance { get; set; } = 3;            // how far from river the boost extends
    public float HeightRetentionFactor { get; set; } = 0.5f;  // 0=flat, 1=high ground holds no nutrients
}

public sealed class MetabolismConfig
{
    public float EnergyInitial { get; set; } = 100f;
    public float EnergyDecayBase { get; set; } = 0.1f;
    public float ColdEnergyDecayMax { get; set; } = 0.08f;
    public float WaterInitial { get; set; } = 100f;
    public float WaterDecayRate { get; set; } = 0.1f;
    public float DrinkRestore { get; set; } = 40f;
    public float DehydrationThreshold { get; set; } = 30f;
    public float DehydrationEnergyPenalty { get; set; } = 0.3f;
    public float MoveCost { get; set; } = 0.5f;
    public float AttackCostBase { get; set; } = 8f;
    public float EmitCost { get; set; } = 3f;
    public float ShallowWaterMoveExtra { get; set; } = 1f;
    public float DeepWaterMoveExtra { get; set; } = 2.5f;
    public float DeepWaterClimbExtra { get; set; } = 1f;
    public float LowEnergyThreshold { get; set; } = 10f;
    public int StationaryTicksRequired { get; set; } = 5;
    public float StationaryDamageMult { get; set; } = 1.2f;
    public float StationarySelfHeat { get; set; } = 0.5f;
    public float StationaryNeighborHeat { get; set; } = 0.3f;
    public float SlopeMoveExp { get; set; } = 0.5f;    // moveCost *= exp(slope * k), continuous penalty
    public float VisionHeightBonus { get; set; } = 0.15f; // effective vision += height/255 * k
    public float FatMetabolismMult { get; set; } = 0.5f;   // base decay *= (1 - fat * k), high fat = less decay
    public float FatSpeedBonus { get; set; } = 0.3f;       // move cost *= (1 + (fat-0.5)*k), low fat = faster
}

public sealed class WindConfig
{
    public float PressureTempFactor { get; set; } = 0.5f;  // pressure drop per °C deviation from avg
    public float WindStrength { get; set; } = 1.0f;        // global wind multiplier
    public float AdvectionRate { get; set; } = 0.05f;      // temperature advection per tick
    public float ScentAdvectionRate { get; set; } = 0.08f; // scent/food-scent advection per tick
    public float EvaporationWindMult { get; set; } = 1.5f; // evaporation multiplier at wind speed 1
    public float SeedWindDrift { get; set; } = 0.5f;       // seed dispersal wind influence (0=random, 1=pure downwind)
}

public sealed class SunlightConfig
{
    public float PeakIntensity { get; set; } = 1.0f;       // noon solar intensity (0-1)
    public float AspectSunMult { get; set; } = 1.5f;       // south-face sunlight multiplier
    public float SolarHeatingRate { get; set; } = 0.03f;   // °C/tick max solar heating
    public float SunEvaporationMult { get; set; } = 1.5f;   // evaporation multiplier at peak sun
    public float SunPhotosynthesisBoost { get; set; } = 2.0f; // plant growth multiplier at peak sun
}

public sealed class BiomeConfig
{
    // Thresholds for biome derivation
    public float DesertAridityMax { get; set; } = 0.12f;    // groundwater below this → desert
    public float GrasslandAridityMax { get; set; } = 0.30f;  // groundwater below this → grassland
    public float WetlandWaterMin { get; set; } = 0.25f;      // surface water or groundwater above this → wetland
    public float JungleNutrientMin { get; set; } = 3f;       // nutrient above this + warm + wet → jungle
    public float JungleTempMin { get; set; } = 18f;          // temperature above this for jungle
    public float HighlandHeightMin { get; set; } = 180f;     // height above this (/255) → highland
    public float ValleyHeightMax { get; set; } = 70f;        // height below this → valley
    public int RiverBankDistance { get; set; } = 3;          // distance to river for riverbank biome

    // Per-biome modifiers (applied multiplicatively on top of grid variables)
    public float DesertEvaporationMult { get; set; } = 2.0f;
    public float DesertBodyTempRateMult { get; set; } = 1.3f;
    public float WetlandEvaporationMult { get; set; } = 0.6f;
    public float WetlandBodyTempRateMult { get; set; } = 0.7f;
    public float JunglePlantGrowthMult { get; set; } = 1.5f;
    public float GrasslandPlantGrowthMult { get; set; } = 1.2f;
    public float DesertPlantGrowthMult { get; set; } = 0.3f;
    public float HighlandBodyTempRateMult { get; set; } = 1.2f;
}


