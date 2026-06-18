using ZHI.Shared;

namespace ZHI.Watcher;

public partial class CosmosEngine
{
    private int _nextRainTick;
    private float _humidity = 0.5f;

    public float Humidity => _humidity;

    private void ApplyWaterCycle()
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        bool isDaytime = _gameTimeOfDay >= 6f && _gameTimeOfDay < 20f;

        ApplyRainfall(W, H);
        ApplyEvaporation(W, H, isDaytime);
        ApplySurfaceFlow(W, H);
        ApplyGroundwaterExchange(W, H);
        ApplyGroundwaterDiffusion(W, H);
    }

    private void ApplyRainfall(int W, int H)
    {
        if (_globalTick < _nextRainTick) return;

        float rainAmount = _config.WaterCycle.RainAmount * (0.5f + _humidity);
        int radius = _config.WaterCycle.RainRadius;

        int cx = _rng.Next(W);
        int cy = _rng.Next(H);

        for (int dx = -radius; dx <= radius; dx++)
            for (int dy = -radius; dy <= radius; dy++)
            {
                int x = cx + dx, y = cy + dy;
                if (x < 0 || x >= W || y < 0 || y >= H) continue;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                float falloff = MathF.Max(0, 1f - dist / radius);
                _v.SurfaceWaterGrid[x, y] = MathF.Min(
                    _config.WaterCycle.SurfaceWaterMaxDepth,
                    _v.SurfaceWaterGrid[x, y] + rainAmount * falloff);
            }

        _humidity = MathF.Min(1f, _humidity + 0.1f);

        _nextRainTick = _globalTick + _rng.Next(
            _config.WaterCycle.RainIntervalMin,
            _config.WaterCycle.RainIntervalMax);

        _tickEvents.Add(new WorldEvent { Type = "rain", AgentId = -1, Value = cx * 1000 + cy, Tick = _globalTick });
    }

    private void ApplyEvaporation(int W, int H, bool isDaytime)
    {
        if (!isDaytime) return;

        float evapRate = _config.WaterCycle.EvaporationRate;
        float tempFactor = MathF.Max(0, (_temperature - 5f) / 30f);

        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                float water = _v.SurfaceWaterGrid[x, y];
                if (water <= 0) continue;

                float evap = evapRate * tempFactor * water;
                _v.SurfaceWaterGrid[x, y] = MathF.Max(0, water - evap);
                _humidity = MathF.Min(1f, _humidity + evap * 0.01f);
            }
    }

    private void ApplySurfaceFlow(int W, int H)
    {
        float flowRate = _config.WaterCycle.SurfaceFlowRate;
        var newGrid = new float[W, H];

        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                float water = _v.SurfaceWaterGrid[x, y];
                if (water <= 0.01f) continue;

                float outflow = water * flowRate;
                // Steep slopes accelerate surface runoff
                float slopeFactor = 1f + _v.Slope[x, y] * _config.WaterCycle.SlopeRunoffMult;
                outflow *= slopeFactor;
                float totalOutflow = 0f;
                float myHeight = _v.HeightMap[x, y];

                var lower = new List<(int nx, int ny, float diff)>();
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || nx >= W || ny < 0 || ny >= H) continue;
                        float nHeight = _v.HeightMap[nx, ny];
                        if (nHeight < myHeight)
                            lower.Add((nx, ny, myHeight - nHeight));
                    }

                if (lower.Count == 0) { newGrid[x, y] += water; continue; }

                float totalDiff = lower.Sum(n => n.diff);
                foreach (var (nx, ny, diff) in lower)
                {
                    float share = outflow * (diff / totalDiff);
                    newGrid[nx, ny] += share;
                    totalOutflow += share;
                }

                newGrid[x, y] += water - totalOutflow;
            }

        float maxDepth = _config.WaterCycle.SurfaceWaterMaxDepth;
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
                newGrid[x, y] = MathF.Min(maxDepth, newGrid[x, y]);

        _v.SurfaceWaterGrid = newGrid;
    }

    private void ApplyGroundwaterExchange(int W, int H)
    {
        float absRate = _config.WaterCycle.AbsorptionRate;
        float maxGround = _config.WaterCycle.MaxGroundwater;

        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                float surface = _v.SurfaceWaterGrid[x, y];
                float ground = _v.GroundwaterGrid[x, y];

                if (surface > 0 && ground < maxGround)
                {
                    float absorb = MathF.Min(absRate * surface, maxGround - ground);
                    _v.SurfaceWaterGrid[x, y] -= absorb;
                    _v.GroundwaterGrid[x, y] += absorb / maxGround;
                }
                else if (surface < 0.01f && ground > 0.01f)
                {
                    float seep = absRate * 0.1f * ground;
                    _v.GroundwaterGrid[x, y] -= seep;
                    _v.SurfaceWaterGrid[x, y] += seep * maxGround;
                }
            }
    }

    private void ApplyGroundwaterDiffusion(int W, int H)
    {
        float rate = _config.WaterCycle.GroundwaterDiffusionRate;
        var newGrid = new float[W, H];

        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                float current = _v.GroundwaterGrid[x, y];
                float outflow = current * rate;
                float inflow = 0f;
                int nc = 0;
                if (x > 0) { inflow += _v.GroundwaterGrid[x - 1, y] * rate; nc++; }
                if (x < W - 1) { inflow += _v.GroundwaterGrid[x + 1, y] * rate; nc++; }
                if (y > 0) { inflow += _v.GroundwaterGrid[x, y - 1] * rate; nc++; }
                if (y < H - 1) { inflow += _v.GroundwaterGrid[x, y + 1] * rate; nc++; }
                newGrid[x, y] = Math.Clamp(current - outflow + inflow / Math.Max(1, nc), 0, 1);
            }
        _v.GroundwaterGrid = newGrid;
    }
}
