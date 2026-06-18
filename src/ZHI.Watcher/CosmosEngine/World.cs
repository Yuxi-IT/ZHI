using ZHI.Shared;

namespace ZHI.Watcher;

public partial class CosmosEngine
{
    /// <summary>Convert dx,dy delta to 8-direction byte: 1=N, 2=NE, 3=E, 4=SE, 5=S, 6=SW, 7=W, 8=NW.</summary>
    private static byte DeltaToFlowDir(int dx, int dy)
    {
        // clamp to -1,0,1 and normalize
        int sx = Math.Sign(dx), sy = Math.Sign(dy);
        return (sx, sy) switch
        {
            (0, -1) => 1,  // N
            (1, -1) => 2,  // NE
            (1, 0) => 3,   // E
            (1, 1) => 4,   // SE
            (0, 1) => 5,   // S
            (-1, 1) => 6,  // SW
            (-1, 0) => 7,  // W
            (-1, -1) => 8, // NW
            _ => 0
        };
    }

    /// <summary>
    /// Generate continuous height map using layered value noise.
    /// Heights range from 0 (basin) to 255 (peak). Output stored as byte[,].
    /// </summary>
    private void GenerateHeightMap()
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        int seed = _rng.Next();

        float Noise(int x, int y, int octave)
        {
            int period = 8 << octave;
            // Hash-based value noise at grid cell corners with bilinear interpolation
            int cx = x / period, cy = y / period;
            float fx = (float)(x % period) / period;
            float fy = (float)(y % period) / period;

            static float Hash(int sx, int sy, int s)
            {
                int h = sx * 374761393 + sy * 668265263 + s * 1274126177;
                h = (h ^ (h >> 13)) * 1274126177;
                return (float)(h & 0x7fffffff) / 0x7fffffff;
            }

            float v00 = Hash(cx, cy, seed + octave);
            float v10 = Hash(cx + 1, cy, seed + octave);
            float v01 = Hash(cx, cy + 1, seed + octave);
            float v11 = Hash(cx + 1, cy + 1, seed + octave);

            float ix0 = v00 + (v10 - v00) * fx;
            float ix1 = v01 + (v11 - v01) * fx;
            return ix0 + (ix1 - ix0) * fy;
        }

        for (int x = 0; x < W; x++)
        {
            for (int y = 0; y < H; y++)
            {
                float h = 0f;
                float amp = 1f;
                float totalAmp = 0f;
                for (int oct = 0; oct < 4; oct++)
                {
                    h += Noise(x, y, oct) * amp;
                    totalAmp += amp;
                    amp *= 0.5f;
                }
                h /= totalAmp;        // normalize to 0–1
                _v.HeightMap[x, y] = (byte)(h * 255f); // map to 0..255
            }
        }
    }

    private void GenerateRiver()
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        int riverWidth = _config.River.Width;
        int deepWidth = _config.River.DeepWidth;
        int fordChance = _config.River.FordChance;
        int riverCount = _config.River.Count;

        // Guard: clamp river width so Random.Next(min, max) doesn't fail
        int maxRiverWidth = Math.Min(W, H) / 2 - 2;
        if (riverWidth > maxRiverWidth)
        {
            riverWidth = Math.Max(1, maxRiverWidth);
            if (deepWidth > riverWidth) deepWidth = riverWidth;
            Log($"[River] Width clamped to {riverWidth} (grid too small for configured width)");
        }
        if (riverWidth < 1) return;

        for (int r = 0; r < riverCount; r++)
        {
            bool horizontal = _rng.Next(2) == 0;

            if (horizontal)
            {
                int yCenter = _rng.Next(riverWidth, H - riverWidth - 1);
                int prevY = yCenter;
                for (int x = 0; x < W; x++)
                {
                    yCenter += _rng.Next(-1, 2);
                    yCenter = Math.Clamp(yCenter, riverWidth, H - riverWidth - 1);

                    // Flow direction: always E (3) for horizontal river, with ± vertical component
                    int dx = 1, dy = Math.Sign(yCenter - prevY);
                    byte flowDir = dy != 0 ? DeltaToFlowDir(dx, dy) : (byte)3;

                    bool isFord = _rng.Next(100) < fordChance;

                    for (int dy2 = -riverWidth / 2; dy2 <= riverWidth / 2; dy2++)
                    {
                        int y = yCenter + dy2;
                        if (y < 0 || y >= H) continue;

                        int distFromCenter = Math.Abs(dy2);
                        if (!isFord && distFromCenter < deepWidth)
                            _v.RiverGrid[x, y] = 2; // deep
                        else if (_v.RiverGrid[x, y] == 0) // don't overwrite existing deep water
                            _v.RiverGrid[x, y] = 1; // shallow

                        // Propagate flow direction to all cells in river band
                        if (_v.RiverFlow[x, y] == 0)
                            _v.RiverFlow[x, y] = flowDir;
                    }

                    prevY = yCenter;
                }
            }
            else
            {
                int xCenter = _rng.Next(riverWidth, W - riverWidth - 1);
                int prevX = xCenter;
                for (int y = 0; y < H; y++)
                {
                    xCenter += _rng.Next(-1, 2);
                    xCenter = Math.Clamp(xCenter, riverWidth, W - riverWidth - 1);

                    // Flow direction: always S (5) for vertical river, with ± horizontal component
                    int dx = Math.Sign(xCenter - prevX), dy = 1;
                    byte flowDir = dx != 0 ? DeltaToFlowDir(dx, dy) : (byte)5;

                    bool isFord = _rng.Next(100) < fordChance;

                    for (int dx2 = -riverWidth / 2; dx2 <= riverWidth / 2; dx2++)
                    {
                        int x = xCenter + dx2;
                        if (x < 0 || x >= W) continue;

                        int distFromCenter = Math.Abs(dx2);
                        if (!isFord && distFromCenter < deepWidth)
                            _v.RiverGrid[x, y] = 2; // deep
                        else if (_v.RiverGrid[x, y] == 0) // don't overwrite existing deep water
                            _v.RiverGrid[x, y] = 1; // shallow

                        // Propagate flow direction to all cells in river band
                        if (_v.RiverFlow[x, y] == 0)
                            _v.RiverFlow[x, y] = flowDir;
                    }

                    prevX = xCenter;
                }
            }
        }

        Log($"[River] Generated {riverCount} river(s), width={riverWidth}, deep={deepWidth}");
    }

    private void ComputeWaterSound()
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        int range = _config.River.SoundRange;
        float decay = _config.River.SoundDecay;

        Array.Clear(_v.WaterSoundGrid);

        // For each water cell, propagate sound outward
        for (int wx = 0; wx < W; wx++)
        {
            for (int wy = 0; wy < H; wy++)
            {
                if (_v.RiverGrid[wx, wy] == 0) continue;

                // BFS from this water cell
                for (int dx = -range; dx <= range; dx++)
                {
                    for (int dy = -range; dy <= range; dy++)
                    {
                        int tx = wx + dx;
                        int ty = wy + dy;
                        if (tx < 0 || tx >= W || ty < 0 || ty >= H) continue;

                        int dist = Math.Abs(dx) + Math.Abs(dy); // Manhattan distance
                        if (dist > range) continue;

                        float sound = MathF.Pow(decay, dist);
                        _v.WaterSoundGrid[tx, ty] += sound;
                    }
                }
            }
        }
    }

    private bool IsValidFoodPlacement(int x, int y)
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        if (x < 0 || x >= W || y < 0 || y >= H) return false;
        if (_v.RiverGrid[x, y] > 0) return false;
        foreach (var ft in _v.Plants)
            if (ft.X == x && ft.Y == y) return false;
        return true;
    }

    private void SpawnInitialFood()
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        int plantCount = _config.Plant.InitialPlants;
        float plantEnergy = _config.Plant.InitialPlantEnergy;

        for (int i = 0; i < plantCount; i++)
        {
            bool placed = false;
            for (int attempt = 0; attempt < 100; attempt++)
            {
                int x = _rng.Next(W);
                int y = _rng.Next(H);
                if (!IsValidFoodPlacement(x, y)) continue;

                // Mix of stages: 80% adult, 20% sprout (no seeds — too slow to establish)
                var stage = i < plantCount * 0.8f ? PlantStage.Adult : PlantStage.Sprout;
                float energy = stage switch
                {
                    PlantStage.Adult => plantEnergy,
                    PlantStage.Sprout => plantEnergy * 0.6f,
                    _ => plantEnergy
                };

                lock (_v.LockObj)
                    _v.Plants.Add(new PlantTile
                    {
                        X = x, Y = y,
                        Energy = energy,
                        Stage = (byte)stage,
                        Age = stage == PlantStage.Adult ? _rng.Next(1000) : 0,
                        Health = 0.8f
                    });
                placed = true;
                break;
            }
            if (!placed)
                Log($"[Cosmos] WARNING: Could not place plant #{i} after 100 attempts");
        }
    }
}
