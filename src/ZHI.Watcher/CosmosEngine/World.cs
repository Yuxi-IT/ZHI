using ZHI.Shared;

namespace ZHI.Watcher;

public partial class CosmosEngine
{
    /// <summary>Check if cell has any water in its Moore (8) neighborhood.</summary>
    private bool HasAdjacentWater(int x, int y)
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && nx < W && ny >= 0 && ny < H && _v.IsAnyWater(nx, ny))
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Per-tick terrain physics v4.2:
    /// - Weathering: Pit/Mound TTL decrement → Flat when expired (DynamicWater is permanent, -1 TTL).
    /// - Flooding: only Pit floods (Mound/Flat block water). DynamicWater is permanent.
    /// </summary>
    private void SettleTerrainPhysics()
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;

        for (int x = 0; x < W; x++)
        {
            for (int y = 0; y < H; y++)
            {
                byte type = _v.TerrainType[x, y];
                int ttl = _v.TerrainTTL[x, y];

                // 1. Weathering: only Pit and Mound decay (TTL > 0 means active lifespan)
                if ((type == ToolDefinitions.TerrainPit || type == ToolDefinitions.TerrainMound) && ttl > 0)
                {
                    _v.TerrainTTL[x, y] = --ttl;
                    if (ttl == 0)
                    {
                        _v.TerrainType[x, y] = ToolDefinitions.TerrainFlat;
                        _tickEvents.Add(new WorldEvent
                        {
                            Type = "weather", AgentId = -1,
                            FoodType = type == ToolDefinitions.TerrainPit ? "pit" : "mound",
                            Value = x * 1000 + y,
                            Tick = _globalTick
                        });
                    }
                }

                // 2. Flooding: only Pit can be flooded. Mound and Flat block water.
                if (type == ToolDefinitions.TerrainPit && HasAdjacentWater(x, y))
                {
                    _v.TerrainType[x, y] = ToolDefinitions.TerrainDynamicWater;
                    _v.TerrainTTL[x, y] = ToolDefinitions.PermaWaterTTL; // permanent, no evaporation
                    _tickEvents.Add(new WorldEvent
                    {
                        Type = "flood", AgentId = -1,
                        FoodType = "pit_flooded",
                        Value = x * 1000 + y,
                        Tick = _globalTick
                    });
                }
            }
        }
    }

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

    private bool IsValidFoodPlacement(int x, int y, int w, int h)
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        for (int fx = 0; fx < w; fx++)
        {
            for (int fy = 0; fy < h; fy++)
            {
                int tx = x + fx, ty = y + fy;
                if (tx < 0 || tx >= W || ty < 0 || ty >= H) return false;
                // No water tiles
                if (_v.RiverGrid[tx, ty] > 0) return false;
                // No overlap with existing food
                foreach (var ft in _v.FoodTiles)
                {
                    int fw = ft.Width > 0 ? ft.Width : 1;
                    int fh = ft.Height > 0 ? ft.Height : 1;
                    if (tx >= ft.X && tx < ft.X + fw && ty >= ft.Y && ty < ft.Y + fh)
                        return false;
                }
            }
        }
        return true;
    }

    private void SpawnInitialFood()
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;

        // Spawn normal food
        for (int i = 0; i < _config.Grid.InitialFood; i++)
        {
            bool placed = false;
            for (int attempt = 0; attempt < 100; attempt++)
            {
                int x = _rng.Next(W);
                int y = _rng.Next(H);
                if (!IsValidFoodPlacement(x, y, 1, 1)) continue;
                lock (_v.LockObj)
                    _v.FoodTiles.Add(new FoodTile
                    {
                        X = x, Y = y,
                        Width = 1, Height = 1,
                        Energy = _config.Grid.FoodEnergy,
                        IsBig = false
                    });
                placed = true;
                break;
            }
            if (!placed)
                Log($"[Cosmos] WARNING: Could not place food #{i} after 100 attempts");
        }

        // Spawn BigFood (2x2 multi-cell)
        for (int i = 0; i < _config.Grid.InitialBigFood; i++)
        {
            bool placed = false;
            for (int attempt = 0; attempt < 50; attempt++)
            {
                int x = _rng.Next(W - 1);
                int y = _rng.Next(H - 1);
                if (!IsValidFoodPlacement(x, y, 2, 2)) continue;
                lock (_v.LockObj)
                    _v.FoodTiles.Add(new FoodTile
                    {
                        X = x, Y = y,
                        Width = 2, Height = 2,
                        Energy = _config.Grid.BigFoodEnergy,
                        IsBig = true
                    });
                placed = true;
                break;
            }
            if (!placed)
                Log($"[Cosmos] WARNING: Could not place BigFood #{i} after 50 attempts");
        }
    }
}
