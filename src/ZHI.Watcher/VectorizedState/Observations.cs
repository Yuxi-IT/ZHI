using TorchSharp;
using ZHI.Shared;
using static TorchSharp.torch;

namespace ZHI.Watcher;

public partial class VectorizedState
{
    private static readonly bool[,] BaseVisionMask = {
        { true,  true,  true,  true,  true,  true,  true  },
        { false, true,  true,  true,  true,  true,  false },
        { false, false, true,  true,  true,  false, false },
        { false, false, false, true,  false, false, false },
        { false, false, false, false, false, false, false },
        { false, false, false, false, false, false, false },
        { false, false, false, false, false, false, false },
    };

    public void RebuildSpatialGrids()
    {
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        int gridSize = W * H;

        for (int i = 0; i < gridSize; i++) _agentGrid[i] = -1;
        Array.Clear(_plantGrid);
        Array.Clear(_plantStageGrid);
        Array.Clear(_corpseGrid);

        for (int i = 0; i < N; i++)
        {
            if (!Alive[i]) continue;
            int key = PosX[i] * H + PosY[i];
            _agentGrid[key] = i;
        }

        for (int f = 0; f < Plants.Count; f++)
        {
            var p = Plants[f];
            int idx = p.X * H + p.Y;
            if (p.X >= 0 && p.X < W && p.Y >= 0 && p.Y < H)
            {
                _plantGrid[idx] = p.Energy;
                _plantStageGrid[idx] = p.Stage;
            }
        }

        for (int c = 0; c < CorpseTiles.Count; c++)
        {
            var ct = CorpseTiles[c];
            if (ct.X >= 0 && ct.X < W && ct.Y >= 0 && ct.Y < H)
                _corpseGrid[ct.X * H + ct.Y] = true;
        }
    }

    private float[] ComputeAgentVisibility(int cx, int cy, int R, int fd, float heightNorm, float blockMax, int blockDist)
    {
        int D = R * 2 + 1;
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        var vis = new float[D * D]; // row-major: [(dy+R)*D + (dx+R)]

        for (int dy = -R; dy <= R; dy++)
        {
            for (int dx = -R; dx <= R; dx++)
            {
                int cellIdx = (dy + R) * D + (dx + R);

                // Directional mask check
                int rdx, rdy;
                switch (fd)
                {
                    case 0: rdx = dx; rdy = dy; break;
                    case 1: rdx = -dx; rdy = -dy; break;
                    case 2: rdx = -dy; rdy = dx; break;
                    default: rdx = dy; rdy = -dx; break;
                }
                int maskCol = rdx + R, maskRow = rdy + R;
                if (maskCol < 0 || maskCol >= D || maskRow < 0 || maskRow >= D) continue;
                if (!BaseVisionMask[maskRow, maskCol])
                {
                    float rowDepth = maskRow / (float)(D - 1);
                    float visibilityReq = rowDepth * 1.5f;
                    if (heightNorm < visibilityReq - 0.1f) continue;
                }

                int gx = cx + dx, gy = cy + dy;
                if (gx < 0 || gx >= W || gy < 0 || gy >= H) continue;

                int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                if (dist <= 1)
                {
                    vis[cellIdx] = 1f;
                    continue;
                }

                // Ray-cast occlusion: accumulate VisibilityBlock along ray from agent to cell
                float accumulated = 0f;
                for (int t = 1; t < dist; t++)
                {
                    int ix = (int)MathF.Round(dx * t / (float)dist);
                    int iy = (int)MathF.Round(dy * t / (float)dist);
                    int igx = cx + ix, igy = cy + iy;
                    if (igx >= 0 && igx < W && igy >= 0 && igy < H)
                        accumulated += VisibilityBlock[igx, igy];
                }
                vis[cellIdx] = MathF.Max(0f, 1f - accumulated);
            }
        }

        return vis;
    }

    public void ComputeVisibilityBlock()
    {
        int W = ToolDefinitions.GridWidth, H = ToolDefinitions.GridHeight;
        Array.Clear(VisibilityBlock);
        foreach (var plant in Plants)
        {
            float block = plant.Species switch
            {
                (byte)PlantSpecies.Grass => 0f,
                (byte)PlantSpecies.Bush => plant.Stage >= (byte)PlantStage.Adult ? 0.3f : 0f,
                (byte)PlantSpecies.Tree => plant.Stage switch
                {
                    (byte)PlantStage.Sprout => 0.2f,
                    (byte)PlantStage.Adult => 0.8f,
                    (byte)PlantStage.Decay => 0.6f,
                    _ => 0f
                },
                _ => 0f
            };
            if (block > 0)
                VisibilityBlock[plant.X, plant.Y] = MathF.Max(VisibilityBlock[plant.X, plant.Y], block);
        }
    }

    public void BuildStateMatrix()
    {
        int S = ToolDefinitions.StateSize; // 340
        int R = ToolDefinitions.VisionRadius;
        int D = R * 2 + 1; // 7
        int W = ToolDefinitions.GridWidth;
        int H = ToolDefinitions.GridHeight;
        const int GridCh = 6; // plant_energy, groundwater, corpse, agent, self, terrain

        Array.Clear(_stateAssemblyBuffer);
        float blockMax = PlantMaxEnergy > 0 ? 0.8f : 0; // will be set from config; default 0.8f
        int blockDist = 3;

        for (int i = 0; i < N; i++)
        {
            if (!Alive[i]) continue;

            int baseIdx = i * S;
            int cx = PosX[i];
            int cy = PosY[i];

            int gridBase = baseIdx;
            int fd = FacingDirection[i];
            float heightNorm = HeightMap[cx, cy] / 255f;

            // Precompute per-agent visibility
            float[] cellVis = ComputeAgentVisibility(cx, cy, R, fd, heightNorm, blockMax, blockDist);

            // [0-293] 7x7 grid x 6 channels
            for (int dy = -R; dy <= R; dy++)
            {
                for (int dx = -R; dx <= R; dx++)
                {
                    int cellIdx = ((dy + R) * D + (dx + R)) * GridCh;
                    int visIdx = (dy + R) * D + (dx + R);
                    float vis = cellVis[visIdx];
                    if (vis < 0.05f) continue; // fully occluded

                    int gx = cx + dx, gy = cy + dy;
                    if (gx < 0 || gx >= W || gy < 0 || gy >= H) continue;

                    bool hasCorpse = HasCorpseAt(gx, gy);
                    bool hasAgent = HasOtherAgentAt(i, gx, gy);
                    bool isSelf = (dx == 0 && dy == 0);
                    float cellHeightNorm = HeightMap[gx, gy] / 255f;

                    _stateAssemblyBuffer[gridBase + cellIdx + 0] = GetPlantEnergyAt(gx, gy) / PlantMaxEnergy * vis;
                    _stateAssemblyBuffer[gridBase + cellIdx + 1] = GroundwaterGrid[gx, gy] * vis;
                    _stateAssemblyBuffer[gridBase + cellIdx + 2] = (hasCorpse ? 1f : 0f) * vis;
                    _stateAssemblyBuffer[gridBase + cellIdx + 3] = ((hasAgent && !isSelf) ? 1f : 0f) * vis;
                    _stateAssemblyBuffer[gridBase + cellIdx + 4] = isSelf ? 1f : 0f;
                    _stateAssemblyBuffer[gridBase + cellIdx + 5] = cellHeightNorm * vis;
                }
            }

            // ── Non-grid state [294-339] (46 values) ──

            // [294-298] vital signs
            _stateAssemblyBuffer[baseIdx + 294] = Energy[i] / 100f;
            _stateAssemblyBuffer[baseIdx + 295] = Stress[i] / 5f;
            _stateAssemblyBuffer[baseIdx + 296] = LastAction[i] / 7f;
            _stateAssemblyBuffer[baseIdx + 297] = Math.Min(TickCount[i] / 200f, 1f);
            _stateAssemblyBuffer[baseIdx + 298] = 0f; // reserved (was Stamina)

            // [299] chemical memory (continuous 0-1)
            _stateAssemblyBuffer[baseIdx + 299] = ChemicalMemory[i];

            // [300-303] scent gradient
            float scentHere = ScentGrid[cx, cy];
            _stateAssemblyBuffer[baseIdx + 300] = (cy > 0) ? ScentGrid[cx, cy - 1] - scentHere : 0f;
            _stateAssemblyBuffer[baseIdx + 301] = (cy < H - 1) ? ScentGrid[cx, cy + 1] - scentHere : 0f;
            _stateAssemblyBuffer[baseIdx + 302] = (cx < W - 1) ? ScentGrid[cx + 1, cy] - scentHere : 0f;
            _stateAssemblyBuffer[baseIdx + 303] = (cx > 0) ? ScentGrid[cx - 1, cy] - scentHere : 0f;

            // [304-306] local stats (same occlusion as grid obs)
            int foodVisible = 0;
            int agentVisible = 0;
            for (int dy = -R; dy <= R; dy++)
            {
                for (int dx = -R; dx <= R; dx++)
                {
                    int visIdx = (dy + R) * D + (dx + R);
                    float vis = cellVis[visIdx];
                    if (vis < 0.05f) continue;
                    int gx = cx + dx, gy = cy + dy;
                    if (gx < 0 || gx >= W || gy < 0 || gy >= H) continue;
                    int key = gx * H + gy;
                    if ((_plantGrid[key] > 0 && _plantStageGrid[key] != (byte)PlantStage.Seed) || _corpseGrid[key]) foodVisible++;
                    if (dx == 0 && dy == 0) continue;
                    int agentIdx = _agentGrid[key];
                    if (agentIdx >= 0 && agentIdx != i) agentVisible++;
                }
            }
            _stateAssemblyBuffer[baseIdx + 304] = Math.Min(foodVisible / 5f, 1f);
            _stateAssemblyBuffer[baseIdx + 305] = Math.Min(agentVisible / 8f, 1f);
            _stateAssemblyBuffer[baseIdx + 306] = Math.Min(scentHere / 10f, 1f);

            // [307-308] facing direction
            _stateAssemblyBuffer[baseIdx + 307] = fd == 2 ? -1f : fd == 3 ? 1f : 0f;
            _stateAssemblyBuffer[baseIdx + 308] = fd == 0 ? -1f : fd == 1 ? 1f : 0f;

            // [309] chemical age
            _stateAssemblyBuffer[baseIdx + 309] = Math.Min(ChemicalAge[i] / 20f, 1f);

            // [310] body water
            _stateAssemblyBuffer[baseIdx + 310] = BodyWater[i] / 100f;

            // [311] reserved (was Thirst)
            _stateAssemblyBuffer[baseIdx + 311] = 0f;

            // [312] water sound
            _stateAssemblyBuffer[baseIdx + 312] = Math.Min(WaterSoundGrid[cx, cy] / 10f, 1f);

            // [313] is_eating
            _stateAssemblyBuffer[baseIdx + 313] = IsEating[i] ? 1f : 0f;

            // [314] is_stationary
            _stateAssemblyBuffer[baseIdx + 314] = IsStationary[i] ? 1f : 0f;

            // [315-320] body parameters (normalized to 0-1)
            _stateAssemblyBuffer[baseIdx + 315] = (BodySize[i] - 0.3f) / 2.2f;
            _stateAssemblyBuffer[baseIdx + 316] = (BodySpeed[i] - 0.3f) / 2.2f;
            _stateAssemblyBuffer[baseIdx + 317] = (BodyStrength[i] - 0.3f) / 2.2f;
            _stateAssemblyBuffer[baseIdx + 318] = (BodyVision[i] - 0.3f) / 2.2f;
            _stateAssemblyBuffer[baseIdx + 319] = BodyFat[i];
            _stateAssemblyBuffer[baseIdx + 320] = BodyColdResist[i];

            // [321] height at position (0..255 → 0-1)
            _stateAssemblyBuffer[baseIdx + 321] = HeightMap[cx, cy] / 255f;

            // [322-325] chemical field gradient (single channel, 4 directions)
            float chemHere = ChemicalField[cx, cy];
            _stateAssemblyBuffer[baseIdx + 322] = (cy > 0) ? ChemicalField[cx, cy - 1] - chemHere : 0f;
            _stateAssemblyBuffer[baseIdx + 323] = (cy < H - 1) ? ChemicalField[cx, cy + 1] - chemHere : 0f;
            _stateAssemblyBuffer[baseIdx + 324] = (cx < W - 1) ? ChemicalField[cx + 1, cy] - chemHere : 0f;
            _stateAssemblyBuffer[baseIdx + 325] = (cx > 0) ? ChemicalField[cx - 1, cy] - chemHere : 0f;

            // [326-339] reserved (zero-filled by Array.Clear)
        }

        using var cpuData = tensor(_stateAssemblyBuffer, [N, S]);
        StateMatrix.copy_(cpuData);
    }
}
