using System.Text.Json;
using ZHI.Shared;

namespace ZHI.Watcher;

public partial class WebServer
{
    private string BuildCosmosPayload()
    {
        if (_engine == null) return "{}";

        var v = _engine.State;
        int n = _engine.AgentCount;
        var agents = new List<object>(n);
        for (int i = 0; i < n; i++)
        {
            var sigMem = new float[ToolDefinitions.SignalValues];
            for (int ch = 0; ch < ToolDefinitions.SignalValues; ch++)
                sigMem[ch] = v.SignalMemory[i, ch];

            agents.Add(new
            {
                id = i,
                x = v.PosX[i],
                y = v.PosY[i],
                existence = v.Existence[i],
                stress = v.Stress[i],
                hunger = v.Hunger[i],
                thirst = v.Thirst[i],
                body_temperature = v.BodyTemperature[i],
                is_eating = v.IsEating[i],
                is_alive = v.Alive[i],
                status = v.StatusMirror[i],
                last_action = v.LastActionNameMirror[i],
                last_signal = v.LastSignalReceived[i],
                signal_memory = sigMem,
                alive_seconds = v.Alive[i]
                    ? (DateTime.UtcNow - v.BirthTimes[i]).TotalSeconds : 0,
                tick_count = v.TickCount[i],
                attack_count = v.AttackCount[i],
                eat_count = v.EatCount[i],
                food_eat_count = v.FoodEatCount[i],
                bigfood_eat_count = v.BigFoodEatCount[i],
                corpse_eat_count = v.CorpseEatCount[i],
                signal_count = v.SignalCount[i],
                facing_direction = v.FacingDirection[i],
                respawn_count = v.RespawnCount[i],
                stamina = v.Stamina[i],
                is_stationary = v.IsStationary[i],
                push_count = v.PushCount[i],
                terraform_count = v.TerraformCount[i]
            });
        }

        FoodTile[] foodSnap;
        CorpseTile[] corpseSnap;
        lock (v.LockObj)
        {
            foodSnap = v.FoodTiles.ToArray();
            corpseSnap = v.CorpseTiles.ToArray();
        }
        var food = new List<object>(foodSnap.Length);
        foreach (var f in foodSnap)
            food.Add(new { x = f.X, y = f.Y, width = f.Width, height = f.Height, energy = f.Energy, max_energy = f.IsBig ? _engine.CurrentConfig.Grid.BigFoodEnergy : _engine.CurrentConfig.Grid.FoodEnergy, is_big = f.IsBig });

        var corpses = new List<object>(corpseSnap.Length);
        foreach (var c in corpseSnap)
            corpses.Add(new { x = c.X, y = c.Y, energy = c.Energy });

        int gw = ToolDefinitions.GridWidth;
        int gh = ToolDefinitions.GridHeight;
        var river = new int[gw * gh];
        for (int rx = 0; rx < gw; rx++)
            for (int ry = 0; ry < gh; ry++)
                river[ry * gw + rx] = v.RiverGrid[rx, ry];

        var scent = new float[gw * gh];
        for (int sx = 0; sx < gw; sx++)
            for (int sy = 0; sy < gh; sy++)
                scent[sy * gw + sx] = v.ScentGrid[sx, sy];

        var foodScent = new float[gw * gh];
        for (int sx = 0; sx < gw; sx++)
            for (int sy = 0; sy < gh; sy++)
                foodScent[sy * gw + sx] = v.FoodScentGrid[sx, sy];

        var tempGrid = new float[gw * gh];
        for (int tx = 0; tx < gw; tx++)
            for (int ty = 0; ty < gh; ty++)
                tempGrid[ty * gw + tx] = v.TemperatureGrid[tx, ty];

        var signalField = new float[gw * gh];
        for (int sx = 0; sx < gw; sx++)
            for (int sy = 0; sy < gh; sy++)
            {
                float maxSig = 0f;
                for (int ch = 0; ch < 4; ch++)
                    maxSig = Math.Max(maxSig, v.SignalField[sx, sy, ch]);
                signalField[sy * gw + sx] = maxSig;
            }

        var terrain = new int[gw * gh];
        for (int tx = 0; tx < gw; tx++)
            for (int ty = 0; ty < gh; ty++)
                terrain[ty * gw + tx] = v.TerrainType[tx, ty];

        var terrainTtl = new int[gw * gh];
        for (int tx = 0; tx < gw; tx++)
            for (int ty = 0; ty < gh; ty++)
                terrainTtl[ty * gw + tx] = v.TerrainTTL[tx, ty];

        var riverFlow = new int[gw * gh];
        for (int fx = 0; fx < gw; fx++)
            for (int fy = 0; fy < gh; fy++)
                riverFlow[fy * gw + fx] = v.RiverFlow[fx, fy];

        float totalEnergySrc = _engine.GenFoodEnergy + _engine.GenBigFoodEnergy + _engine.GenCorpseEnergy;
        float foodPct = totalEnergySrc > 0 ? _engine.GenFoodEnergy / totalEnergySrc * 100f : 0;
        float bigFoodPct = totalEnergySrc > 0 ? _engine.GenBigFoodEnergy / totalEnergySrc * 100f : 0;
        float corpsePct = totalEnergySrc > 0 ? _engine.GenCorpseEnergy / totalEnergySrc * 100f : 0;

        var stats = new
        {
            attack_rate = _engine.GenTotalTicks > 0 ? (float)_engine.GenAttacks / _engine.GenTotalTicks : 0f,
            food_eaten = _engine.GenFoodEaten,
            bigfood_eaten = _engine.GenBigFoodEaten,
            corpses_eaten = _engine.GenCorpsesEaten,
            energy_source = new
            {
                food_pct = foodPct,
                bigfood_pct = bigFoodPct,
                corpse_pct = corpsePct
            }
        };

        var payload = new
        {
            generation = _engine.Generation,
            total_deaths = _engine.TotalDeaths,
            world_day = _engine.WorldDay,
            time_of_day = _engine.GameTimeOfDay,
            temperature = _engine.Temperature,
            agent_count = n,
            total_energy = _engine.TotalEnergyInWorld,
            tick_exceptions = _engine.TickExceptionCount,
            agents,
            food,
            corpses,
            river,
            scent,
            food_scent = foodScent,
            temperature_grid = tempGrid,
            signal_field = signalField,
            terrain,
            terrain_ttl = terrainTtl,
            river_flow = riverFlow,
            grid_width = ToolDefinitions.GridWidth,
            grid_height = ToolDefinitions.GridHeight,
            stats
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
    }
}

internal record SpeedPayload(int Multiplier);
