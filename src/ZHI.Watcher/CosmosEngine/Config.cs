using System.Text.Json;
using ZHI.Shared;

namespace ZHI.Watcher;

public partial class CosmosEngine
{
    private void SaveConfig()
    {
        var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
        File.WriteAllText(_configPath, json);
    }

    public void UpdateConfigAndRestart(ZhiConfig newConfig)
    {
        var json = JsonSerializer.Serialize(newConfig);
        var updated = JsonSerializer.Deserialize<ZhiConfig>(json)!;
        updated.DeathCount = _totalDeaths;

        typeof(ZhiConfig).GetProperty("Cosmos")!.SetValue(_config, updated.Cosmos);
        typeof(ZhiConfig).GetProperty("Grid")!.SetValue(_config, updated.Grid);
        typeof(ZhiConfig).GetProperty("Combat")!.SetValue(_config, updated.Combat);
        typeof(ZhiConfig).GetProperty("Hunger")!.SetValue(_config, updated.Hunger);
        typeof(ZhiConfig).GetProperty("Thirst")!.SetValue(_config, updated.Thirst);
        typeof(ZhiConfig).GetProperty("Existence")!.SetValue(_config, updated.Existence);
        typeof(ZhiConfig).GetProperty("Reproduce")!.SetValue(_config, updated.Reproduce);
        typeof(ZhiConfig).GetProperty("Temperature")!.SetValue(_config, updated.Temperature);
        typeof(ZhiConfig).GetProperty("Scent")!.SetValue(_config, updated.Scent);
        typeof(ZhiConfig).GetProperty("FoodScent")!.SetValue(_config, updated.FoodScent);
        typeof(ZhiConfig).GetProperty("Corpse")!.SetValue(_config, updated.Corpse);
        typeof(ZhiConfig).GetProperty("River")!.SetValue(_config, updated.River);
        typeof(ZhiConfig).GetProperty("Chemical")!.SetValue(_config, updated.Chemical);
        typeof(ZhiConfig).GetProperty("AgeDeath")!.SetValue(_config, updated.AgeDeath);
        typeof(ZhiConfig).GetProperty("Network")!.SetValue(_config, updated.Network);
        typeof(ZhiConfig).GetProperty("DecisionIntervalMs")!.SetValue(_config, updated.DecisionIntervalMs);

        SaveConfig();
        _pendingConfig = updated;
        _pendingRestart = true;
        Log("[Cosmos] Config saved, restart pending...");
    }
}
