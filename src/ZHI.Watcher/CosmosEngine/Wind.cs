using ZHI.Shared;

namespace ZHI.Watcher;

public partial class CosmosEngine
{
    private void ApplyWind()
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;

        // Compute pressure and wind from current temperature field
        _v.ComputePressureAndWind(_temperature, _config.Wind.PressureTempFactor, _config.Wind.WindStrength);

        // Temperature advection: wind carries heat downwind
        ApplyTemperatureAdvection(W, H);

        // Scent advection is handled in ApplyScentPhysics to avoid double iteration
    }

    private void ApplyTemperatureAdvection(int W, int H)
    {
        float rate = _config.Wind.AdvectionRate;
        if (rate <= 0f) return;

        var newGrid = new float[W, H];
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                // Semi-Lagrangian: advect temperature from upwind cell
                float wx = _v.WindX[x, y], wy = _v.WindY[x, y];
                float windMag = MathF.Sqrt(wx * wx + wy * wy);
                if (windMag < 0.01f) { newGrid[x, y] = _v.TemperatureGrid[x, y]; continue; }

                // Upwind displacement (fractional cell)
                float srcX = x - wx * rate;
                float srcY = y - wy * rate;
                int ix = (int)MathF.Floor(srcX), iy = (int)MathF.Floor(srcY);
                float fx = srcX - ix, fy = srcY - iy;

                // Bilinear interpolation from upwind position
                float v00 = SampleTemp(ix, iy), v10 = SampleTemp(ix + 1, iy);
                float v01 = SampleTemp(ix, iy + 1), v11 = SampleTemp(ix + 1, iy + 1);
                newGrid[x, y] = (1 - fx) * (1 - fy) * v00 + fx * (1 - fy) * v10
                              + (1 - fx) * fy * v01 + fx * fy * v11;
            }
        _v.TemperatureGrid = newGrid;
    }

    private float SampleTemp(int x, int y)
    {
        int W = ToolDefinitions.GridWidth, H = ToolDefinitions.GridHeight;
        x = Math.Clamp(x, 0, W - 1);
        y = Math.Clamp(y, 0, H - 1);
        return _v.TemperatureGrid[x, y];
    }

    /// <summary>
    /// Apply wind advection to a scalar grid (used for scent and food scent).
    /// </summary>
    public void AdvectScalarField(float[,] field, float rate)
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        if (rate <= 0f) return;

        var newGrid = new float[W, H];
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                float wx = _v.WindX[x, y], wy = _v.WindY[x, y];
                float windMag = MathF.Sqrt(wx * wx + wy * wy);
                if (windMag < 0.01f) { newGrid[x, y] = field[x, y]; continue; }

                float srcX = x - wx * rate;
                float srcY = y - wy * rate;
                int ix = (int)MathF.Floor(srcX), iy = (int)MathF.Floor(srcY);
                float fx = srcX - ix, fy = srcY - iy;

                int W0 = W, H0 = H;
                float v00 = field[Math.Clamp(ix, 0, W0 - 1), Math.Clamp(iy, 0, H0 - 1)];
                float v10 = field[Math.Clamp(ix + 1, 0, W0 - 1), Math.Clamp(iy, 0, H0 - 1)];
                float v01 = field[Math.Clamp(ix, 0, W0 - 1), Math.Clamp(iy + 1, 0, H0 - 1)];
                float v11 = field[Math.Clamp(ix + 1, 0, W0 - 1), Math.Clamp(iy + 1, 0, H0 - 1)];
                newGrid[x, y] = (1 - fx) * (1 - fy) * v00 + fx * (1 - fy) * v10
                              + (1 - fx) * fy * v01 + fx * fy * v11;
            }
        Array.Copy(newGrid, field, W * H);
    }
}
