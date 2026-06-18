using System.Text.Json;
using ZHI.Shared;

namespace ZHI.Tests;

public class ConfigSerializationTests
{
    [Fact]
    public void RoundTrip_DefaultConfig_PreservesAllValues()
    {
        var original = new ZhiConfig();
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        };

        var json = JsonSerializer.Serialize(original, options);
        var deserialized = JsonSerializer.Deserialize<ZhiConfig>(json, options)!;

        Assert.NotNull(deserialized);
        Assert.Equal(original.Grid.Width, deserialized.Grid.Width);
        Assert.Equal(original.Grid.Height, deserialized.Grid.Height);
        Assert.Equal(original.Grid.MaxAgents, deserialized.Grid.MaxAgents);
        Assert.Equal(original.Grid.InitialFood, deserialized.Grid.InitialFood);
        Assert.Equal(original.Grid.FoodEnergy, deserialized.Grid.FoodEnergy);
        Assert.Equal(original.Grid.BigFoodEnergy, deserialized.Grid.BigFoodEnergy);
        Assert.Equal(original.Grid.FoodDecayPerTick, deserialized.Grid.FoodDecayPerTick);
        Assert.Equal(original.Grid.FoodPerTickEnergy, deserialized.Grid.FoodPerTickEnergy);
        Assert.Equal(original.Grid.FoodRespawnInterval, deserialized.Grid.FoodRespawnInterval);
        Assert.Equal(original.Grid.MaxFood, deserialized.Grid.MaxFood);

        Assert.Equal(original.Cosmos.AgentCount, deserialized.Cosmos.AgentCount);
        Assert.Equal(original.Cosmos.MutationStd, deserialized.Cosmos.MutationStd);
        Assert.Equal(original.Cosmos.RespawnDelayTicks, deserialized.Cosmos.RespawnDelayTicks);

        Assert.Equal(original.Combat.AttackRange, deserialized.Combat.AttackRange);
        Assert.Equal(original.Combat.StressPerAttack, deserialized.Combat.StressPerAttack);
        Assert.Equal(original.Combat.StressDamage, deserialized.Combat.StressDamage);
        Assert.Equal(original.Combat.AttackCost, deserialized.Combat.AttackCost);

        Assert.Equal(original.Hunger.DecayRate, deserialized.Hunger.DecayRate);
        Assert.Equal(original.Hunger.PenaltyStart, deserialized.Hunger.PenaltyStart);
        Assert.Equal(original.Hunger.MaxPenalty, deserialized.Hunger.MaxPenalty);
        Assert.Equal(original.Hunger.Initial, deserialized.Hunger.Initial);

        Assert.Equal(original.Thirst.DecayRate, deserialized.Thirst.DecayRate);
        Assert.Equal(original.Thirst.DrinkRestore, deserialized.Thirst.DrinkRestore);
        Assert.Equal(original.Thirst.PenaltyStart, deserialized.Thirst.PenaltyStart);
        Assert.Equal(original.Thirst.MaxPenalty, deserialized.Thirst.MaxPenalty);
        Assert.Equal(original.Thirst.Initial, deserialized.Thirst.Initial);

        Assert.Equal(original.Temperature.MaxTemp, deserialized.Temperature.MaxTemp);
        Assert.Equal(original.Temperature.MinTemp, deserialized.Temperature.MinTemp);
        Assert.Equal(original.Temperature.ColdThreshold, deserialized.Temperature.ColdThreshold);
        Assert.Equal(original.Temperature.HotThreshold, deserialized.Temperature.HotThreshold);
        Assert.Equal(original.Temperature.AgentBodyHeat, deserialized.Temperature.AgentBodyHeat);
        Assert.Equal(original.Temperature.ThermalDiffusionRate, deserialized.Temperature.ThermalDiffusionRate);
        Assert.Equal(original.Temperature.WaterHeatCapacity, deserialized.Temperature.WaterHeatCapacity);
        Assert.Equal(original.Temperature.HypothermiaThreshold, deserialized.Temperature.HypothermiaThreshold);

        Assert.Equal(original.River.Count, deserialized.River.Count);
        Assert.Equal(original.River.Width, deserialized.River.Width);
        Assert.Equal(original.River.FordChance, deserialized.River.FordChance);

        Assert.Equal(original.Reproduce.MinExistence, deserialized.Reproduce.MinExistence);
        Assert.Equal(original.Reproduce.MinAge, deserialized.Reproduce.MinAge);
        Assert.Equal(original.Reproduce.Cooldown, deserialized.Reproduce.Cooldown);
        Assert.Equal(original.Reproduce.MutationScale, deserialized.Reproduce.MutationScale);

        Assert.Equal(original.AgeDeath.MaxAge, deserialized.AgeDeath.MaxAge);
        Assert.Equal(original.AgeDeath.Stage1Age, deserialized.AgeDeath.Stage1Age);
        Assert.Equal(original.AgeDeath.Stage2Decay, deserialized.AgeDeath.Stage2Decay);
        Assert.Equal(original.Chemical.EmissionCost, deserialized.Chemical.EmissionCost);
        Assert.Equal(original.Chemical.WaveRadius, deserialized.Chemical.WaveRadius);

        Assert.Equal(original.Stamina.MaxStamina, deserialized.Stamina.MaxStamina);
        Assert.Equal(original.Stamina.AttackCost, deserialized.Stamina.AttackCost);
        Assert.Equal(original.Stamina.BaseRecovery, deserialized.Stamina.BaseRecovery);
        Assert.Equal(original.Stamina.StationaryTicksRequired, deserialized.Stamina.StationaryTicksRequired);

        Assert.Equal(original.Corpse.Energy, deserialized.Corpse.Energy);
        Assert.Equal(original.Corpse.DecayPerTick, deserialized.Corpse.DecayPerTick);
    }

    [Fact]
    public void Deserialize_CustomFoodEnergy_OverridesDefault()
    {
        var json = """{"grid":{"food_energy":42},"cosmos":{},"temperature":{},"combat":{},"hunger":{},"thirst":{},"river":{},"existence":{},"reproduce":{},"age_death":{},"chemical":{},"scent":{},"food_scent":{},"network":{},"corpse":{},"stamina":{}}""";
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        var config = JsonSerializer.Deserialize<ZhiConfig>(json, options)!;

        Assert.Equal(42f, config.Grid.FoodEnergy);
        Assert.Equal(64, config.Grid.Width); // untouched default
    }

    [Fact]
    public void Deserialize_PartialConfig_UsesDefaultsForMissingFields()
    {
        var json = """{"grid":{"width":128,"height":128}}""";
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        var config = JsonSerializer.Deserialize<ZhiConfig>(json, options)!;

        Assert.Equal(128, config.Grid.Width);
        Assert.Equal(128, config.Grid.Height);
        Assert.Equal(15f, config.Grid.FoodEnergy); // default untouched
        Assert.Equal(80f, config.Grid.BigFoodEnergy);
        Assert.Equal(64, config.Cosmos.AgentCount);
    }
}
