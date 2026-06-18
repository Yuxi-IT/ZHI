using TorchSharp;
using ZHI.Shared;
using static TorchSharp.torch;

namespace ZHI.Watcher;

public partial class VectorizedState
{
    // Forward-facing visibility mask (facing up, 7x7). 1=visible, 0=hidden.
    // Agent at center (row 3). Can see forward cone, nothing behind.
    private static readonly bool[,] BaseVisionMask = {
        { true,  true,  true,  true,  true,  true,  true  },  // dy=-3
        { false, true,  true,  true,  true,  true,  false },  // dy=-2
        { false, false, true,  true,  true,  false, false },  // dy=-1
        { false, false, false, true,  false, false, false },  // dy=0 (self)
        { false, false, false, false, false, false, false },  // dy=+1
        { false, false, false, false, false, false, false },  // dy=+2
        { false, false, false, false, false, false, false },  // dy=+3
    };

    public void RebuildSpatialGrids()
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        int gridSize = W * H;

        for (int i = 0; i < gridSize; i++) _agentGrid[i] = -1;
        Array.Clear(_foodGrid);
        Array.Clear(_corpseGrid);

        for (int i = 0; i < N; i++)
        {
            if (!Alive[i]) continue;
            int key = PosX[i] * H + PosY[i];
            _agentGrid[key] = i;
        }

        for (int f = 0; f < FoodTiles.Count; f++)
        {
            var ft = FoodTiles[f];
            int fw = ft.Width > 0 ? ft.Width : 1;
            int fh = ft.Height > 0 ? ft.Height : 1;
            byte val = (byte)(ft.IsBig ? 2 : 1);
            for (int dx = 0; dx < fw; dx++)
                for (int dy = 0; dy < fh; dy++)
                {
                    int x = ft.X + dx, y = ft.Y + dy;
                    if (x >= 0 && x < W && y >= 0 && y < H)
                        _foodGrid[x * H + y] = val;
                }
        }

        for (int c = 0; c < CorpseTiles.Count; c++)
        {
            var ct = CorpseTiles[c];
            if (ct.X >= 0 && ct.X < W && ct.Y >= 0 && ct.Y < H)
                _corpseGrid[ct.X * H + ct.Y] = true;
        }
    }

    public void BuildStateMatrix()
    {
        int S = ToolDefinitions.StateSize; // 334
        int R = ToolDefinitions.VisionRadius;
        int D = R * 2 + 1; // 7
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        const int GridCh = 6; // food, bigfood, corpse, agent, self, terrain

        Array.Clear(_stateAssemblyBuffer);

        for (int i = 0; i < N; i++)
        {
            if (!Alive[i]) continue;

            int baseIdx = i * S;
            int cx = PosX[i];
            int cy = PosY[i];

            int gridBase = baseIdx;
            int fd = FacingDirection[i];
            bool agentInPit = TerrainType[cx, cy] == ToolDefinitions.TerrainPit;
            bool agentOnMound = TerrainType[cx, cy] == ToolDefinitions.TerrainMound;

            // [0-293] 7x7 grid x 6 channels: food, bigfood, corpse, agent, self, terrain
            for (int dy = -R; dy <= R; dy++)
            {
                for (int dx = -R; dx <= R; dx++)
                {
                    int cellIdx = ((dy + R) * D + (dx + R)) * GridCh;

                    if (agentInPit && Math.Max(Math.Abs(dx), Math.Abs(dy)) > 1)
                        continue;

                    if (!agentOnMound)
                    {
                        int rdx, rdy;
                        switch (fd)
                        {
                            case 0: rdx = dx; rdy = dy; break;
                            case 1: rdx = -dx; rdy = -dy; break;
                            case 2: rdx = -dy; rdy = dx; break;
                            default: rdx = dy; rdy = -dx; break;
                        }
                        int maskCol = rdx + R, maskRow = rdy + R;
                        if (maskCol < 0 || maskCol >= D || maskRow < 0 || maskRow >= D
                            || !BaseVisionMask[maskRow, maskCol])
                            continue;
                    }

                    int gx = cx + dx, gy = cy + dy;
                    if (gx < 0 || gx >= W || gy < 0 || gy >= H) continue;

                    bool hasFood = HasFoodAt(gx, gy, out bool isBigFood);
                    bool hasCorpse = HasCorpseAt(gx, gy);
                    bool hasAgent = HasOtherAgentAt(i, gx, gy);
                    bool isSelf = (dx == 0 && dy == 0);
                    byte terrain = TerrainType[gx, gy];
                    float terrainNorm = terrain == 1 ? 0.33f : terrain == 2 ? 0.66f : terrain >= 3 || RiverGrid[gx, gy] > 0 ? 1f : 0f;

                    _stateAssemblyBuffer[gridBase + cellIdx + 0] = (hasFood && !isBigFood) ? 1f : 0f;
                    _stateAssemblyBuffer[gridBase + cellIdx + 1] = isBigFood ? 1f : 0f;
                    _stateAssemblyBuffer[gridBase + cellIdx + 2] = hasCorpse ? 1f : 0f;
                    _stateAssemblyBuffer[gridBase + cellIdx + 3] = (hasAgent && !isSelf) ? 1f : 0f;
                    _stateAssemblyBuffer[gridBase + cellIdx + 4] = isSelf ? 1f : 0f;
                    _stateAssemblyBuffer[gridBase + cellIdx + 5] = terrainNorm;
                }
            }

            // [294-298] self state: HP, Stress, LastAction, Age, Stamina
            _stateAssemblyBuffer[baseIdx + 294] = Existence[i] / 100f;
            _stateAssemblyBuffer[baseIdx + 295] = Stress[i] / 5f;
            _stateAssemblyBuffer[baseIdx + 296] = LastAction[i] / 9f;
            _stateAssemblyBuffer[baseIdx + 297] = Math.Min(TickCount[i] / 200f, 1f);
            _stateAssemblyBuffer[baseIdx + 298] = Stamina[i] / 100f;

            // [299-302] signal memory (4 channels)
            _stateAssemblyBuffer[baseIdx + 299] = SignalMemory[i, 0];
            _stateAssemblyBuffer[baseIdx + 300] = SignalMemory[i, 1];
            _stateAssemblyBuffer[baseIdx + 301] = SignalMemory[i, 2];
            _stateAssemblyBuffer[baseIdx + 302] = SignalMemory[i, 3];

            // [303-306] scent gradient
            float scentHere = ScentGrid[cx, cy];
            _stateAssemblyBuffer[baseIdx + 303] = (cy > 0) ? ScentGrid[cx, cy - 1] - scentHere : 0f;
            _stateAssemblyBuffer[baseIdx + 304] = (cy < H - 1) ? ScentGrid[cx, cy + 1] - scentHere : 0f;
            _stateAssemblyBuffer[baseIdx + 305] = (cx < W - 1) ? ScentGrid[cx + 1, cy] - scentHere : 0f;
            _stateAssemblyBuffer[baseIdx + 306] = (cx > 0) ? ScentGrid[cx - 1, cy] - scentHere : 0f;

            // [307-309] local stats (directional visibility)
            int foodVisible = 0;
            int agentVisible = 0;
            for (int dy = -R; dy <= R; dy++)
            {
                for (int dx = -R; dx <= R; dx++)
                {
                    if (agentInPit && Math.Max(Math.Abs(dx), Math.Abs(dy)) > 1)
                        continue;

                    if (!agentOnMound)
                    {
                        int rdx2, rdy2;
                        switch (fd)
                        {
                            case 0: rdx2 = dx; rdy2 = dy; break;
                            case 1: rdx2 = -dx; rdy2 = -dy; break;
                            case 2: rdx2 = -dy; rdy2 = dx; break;
                            default: rdx2 = dy; rdy2 = -dx; break;
                        }
                        int mc = rdx2 + R, mr = rdy2 + R;
                        if (mc < 0 || mc >= D || mr < 0 || mr >= D
                            || !BaseVisionMask[mr, mc])
                            continue;
                    }

                    int gx = cx + dx, gy = cy + dy;
                    if (gx < 0 || gx >= W || gy < 0 || gy >= H) continue;
                    int key = gx * H + gy;
                    if (_foodGrid[key] > 0 || _corpseGrid[key]) foodVisible++;
                    if (dx == 0 && dy == 0) continue;
                    int agentIdx = _agentGrid[key];
                    if (agentIdx >= 0 && agentIdx != i) agentVisible++;
                }
            }
            _stateAssemblyBuffer[baseIdx + 307] = Math.Min(foodVisible / 5f, 1f);
            _stateAssemblyBuffer[baseIdx + 308] = Math.Min(agentVisible / 8f, 1f);
            _stateAssemblyBuffer[baseIdx + 309] = Math.Min(scentHere / 10f, 1f);

            // [310-311] facing direction (unit vector)
            _stateAssemblyBuffer[baseIdx + 310] = fd == 2 ? -1f : fd == 3 ? 1f : 0f;
            _stateAssemblyBuffer[baseIdx + 311] = fd == 0 ? -1f : fd == 1 ? 1f : 0f;

            // [312] signal age
            _stateAssemblyBuffer[baseIdx + 312] = Math.Min(SignalAge[i] / 20f, 1f);

            // [313] hunger
            _stateAssemblyBuffer[baseIdx + 313] = Hunger[i] / 100f;

            // [314] thirst
            _stateAssemblyBuffer[baseIdx + 314] = Thirst[i] / 100f;

            // [315] water sound intensity
            _stateAssemblyBuffer[baseIdx + 315] = Math.Min(WaterSoundGrid[cx, cy] / 10f, 1f);

            // [316] is_eating
            _stateAssemblyBuffer[baseIdx + 316] = IsEating[i] ? 1f : 0f;

            // [317] is_stationary
            _stateAssemblyBuffer[baseIdx + 317] = IsStationary[i] ? 1f : 0f;

            // [318-333] signal field gradient: 4 channels x 4 directions
            for (int ch = 0; ch < 4; ch++)
            {
                float sigHere = SignalField[cx, cy, ch];
                float sigN = (cy > 0) ? SignalField[cx, cy - 1, ch] - sigHere : 0f;
                float sigS = (cy < H - 1) ? SignalField[cx, cy + 1, ch] - sigHere : 0f;
                float sigE = (cx < W - 1) ? SignalField[cx + 1, cy, ch] - sigHere : 0f;
                float sigW = (cx > 0) ? SignalField[cx - 1, cy, ch] - sigHere : 0f;

                _stateAssemblyBuffer[baseIdx + 318 + ch * 4 + 0] = sigN;
                _stateAssemblyBuffer[baseIdx + 318 + ch * 4 + 1] = sigS;
                _stateAssemblyBuffer[baseIdx + 318 + ch * 4 + 2] = sigE;
                _stateAssemblyBuffer[baseIdx + 318 + ch * 4 + 3] = sigW;
            }
        }

        using var cpuData = tensor(_stateAssemblyBuffer, [N, S]);
        StateMatrix.copy_(cpuData);
    }
}
