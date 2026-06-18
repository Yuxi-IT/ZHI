using ZHI.Shared;

namespace ZHI.Watcher;

public partial class CosmosEngine
{
    /// <summary>Get biome-based plant growth multiplier.</summary>
    public float GetBiomePlantGrowthMult(int x, int y)
    {
        var cfg = _config.Biome;
        return _v.Biome[x, y] switch
        {
            ToolDefinitions.BiomeDesert => cfg.DesertPlantGrowthMult,
            ToolDefinitions.BiomeGrassland => cfg.GrasslandPlantGrowthMult,
            ToolDefinitions.BiomeJungle => cfg.JunglePlantGrowthMult,
            _ => 1.0f
        };
    }

    /// <summary>Get biome-based evaporation multiplier.</summary>
    public float GetBiomeEvaporationMult(int x, int y)
    {
        var cfg = _config.Biome;
        return _v.Biome[x, y] switch
        {
            ToolDefinitions.BiomeDesert => cfg.DesertEvaporationMult,
            ToolDefinitions.BiomeWetland => cfg.WetlandEvaporationMult,
            _ => 1.0f
        };
    }

    /// <summary>Get biome-based body temperature lerp rate multiplier.</summary>
    public float GetBiomeBodyTempMult(int x, int y)
    {
        var cfg = _config.Biome;
        return _v.Biome[x, y] switch
        {
            ToolDefinitions.BiomeDesert => cfg.DesertBodyTempRateMult,
            ToolDefinitions.BiomeWetland => cfg.WetlandBodyTempRateMult,
            ToolDefinitions.BiomeHighland => cfg.HighlandBodyTempRateMult,
            _ => 1.0f
        };
    }
}
