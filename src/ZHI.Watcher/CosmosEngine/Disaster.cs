using ZHI.Shared;

namespace ZHI.Watcher;

public enum DisasterKind : byte
{
    None = 0,
    Flood = 1,
    Drought = 2,
    HeatWave = 3,
    ColdSnap = 4
}

public partial class CosmosEngine
{
    private DisasterKind _activeDisaster = DisasterKind.None;
    private int _disasterTicksRemaining;
    private int _lastDisasterTick = -2000;

    // Cached modifiers for water cycle
    public float DisasterEvaporationMult = 1f;
    public float DisasterTempModifier;
    public bool IsFlooding;

    private void ApplyDisasters()
    {
        if (_activeDisaster != DisasterKind.None)
        {
            _disasterTicksRemaining--;
            if (_disasterTicksRemaining <= 0)
            {
                Log($"[Disaster] {_activeDisaster} ended");
                _activeDisaster = DisasterKind.None;
                DisasterEvaporationMult = 1f;
                DisasterTempModifier = 0f;
                IsFlooding = false;
            }
            else if (IsFlooding && _globalTick % 10 == 0)
            {
                TriggerFloodRain();
            }
            return;
        }

        // Cooldown check
        if (_globalTick - _lastDisasterTick < _config.Disaster.Cooldown)
            return;

        if (_rng.NextDouble() >= _config.Disaster.ProbabilityPerTick)
            return;

        // Roll disaster type based on environmental conditions
        var cfg = _config.Disaster;
        int duration = _rng.Next(cfg.MinDuration, cfg.MaxDuration + 1);

        if (_humidity > cfg.FloodHumidityThreshold && _rng.NextDouble() < 0.5)
        {
            _activeDisaster = DisasterKind.Flood;
            IsFlooding = true;
            Log($"[Disaster] FLOOD started ({duration} ticks, humidity={_humidity:F2})");
        }
        else if (_humidity < cfg.DroughtHumidityThreshold && _rng.NextDouble() < 0.5)
        {
            _activeDisaster = DisasterKind.Drought;
            DisasterEvaporationMult = cfg.DroughtEvaporationMult;
            Log($"[Disaster] DROUGHT started ({duration} ticks, humidity={_humidity:F2})");
        }
        else if (_temperature > 25f && _rng.NextDouble() < 0.5)
        {
            _activeDisaster = DisasterKind.HeatWave;
            DisasterTempModifier = cfg.HeatWaveTempBonus;
            Log($"[Disaster] HEATWAVE started ({duration} ticks, temp={_temperature:F1})");
        }
        else
        {
            _activeDisaster = DisasterKind.ColdSnap;
            DisasterTempModifier = -cfg.ColdSnapTempPenalty;
            Log($"[Disaster] COLDSNAP started ({duration} ticks, temp={_temperature:F1})");
        }

        _disasterTicksRemaining = duration;
        _lastDisasterTick = _globalTick;
        _tickEvents.Add(new WorldEvent { Type = "disaster", Value = (int)_activeDisaster, Tick = _globalTick });

        if (IsFlooding)
            TriggerFloodRain();
    }

    private void TriggerFloodRain()
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        var cfg = _config.Disaster;
        int radius = cfg.FloodRainRadius;
        float amount = cfg.FloodRainAmount;
        float maxDepth = _config.WaterCycle.SurfaceWaterMaxDepth;

        for (int cx = 0; cx < W; cx += 8)
            for (int cy = 0; cy < H; cy += 8)
                for (int dx = -radius; dx <= radius; dx++)
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        int x = cx + dx, y = cy + dy;
                        if (x < 0 || x >= W || y < 0 || y >= H) continue;
                        float dist = MathF.Sqrt(dx * dx + dy * dy);
                        float falloff = MathF.Max(0, 1f - dist / radius);
                        _v.SurfaceWaterGrid[x, y] = MathF.Min(maxDepth,
                            _v.SurfaceWaterGrid[x, y] + amount * falloff * 0.5f);
                    }
    }
}
