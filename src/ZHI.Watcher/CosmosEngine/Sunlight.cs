using ZHI.Shared;

namespace ZHI.Watcher;

public partial class CosmosEngine
{
    private void ApplySunlight()
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;

        _v.ComputeSunlight(_gameTimeOfDay, _config.Sunlight.PeakIntensity, _config.Sunlight.AspectSunMult);

        // Solar heating: add heat proportional to sunlight
        float heatRate = _config.Sunlight.SolarHeatingRate;
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
                _v.TemperatureGrid[x, y] += _v.Sunlight[x, y] * heatRate;

        // Sunlight affects plant growth (via photosynthesis boost — applied in PlantGrowth)
        // Sunlight affects evaporation (applied in WaterCycle)
    }
}
