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
            agents.Add(new
            {
                id = i,
                x = v.PosX[i],
                y = v.PosY[i],
                energy = v.Energy[i],
                stress = v.Stress[i],
                water = v.BodyWater[i],
                body_temperature = v.BodyTemperature[i],
                is_eating = v.IsEating[i],
                is_alive = v.Alive[i],
                status = v.StatusMirror[i],
                last_action = v.LastActionNameMirror[i],
                last_signal = v.LastSignalReceived[i],
                chemical_memory = v.ChemicalMemory[i],
                alive_seconds = v.Alive[i]
                    ? (DateTime.UtcNow - v.BirthTimes[i]).TotalSeconds : 0,
                tick_count = v.TickCount[i],
                attack_count = v.AttackCount[i],
                eat_count = v.EatCount[i],
                food_eat_count = v.FoodEatCount[i],
                corpse_eat_count = v.CorpseEatCount[i],
                emit_count = v.EmitCount[i],
                facing_direction = v.FacingDirection[i],
                respawn_count = v.RespawnCount[i],
                is_stationary = v.IsStationary[i]
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
            food.Add(new { x = f.X, y = f.Y, energy = f.Energy, max_energy = _engine.CurrentConfig.Plant.MaxPlantEnergy });

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

        var chemicalField = new float[gw * gh];
        for (int sx = 0; sx < gw; sx++)
            for (int sy = 0; sy < gh; sy++)
                chemicalField[sy * gw + sx] = v.ChemicalField[sx, sy];

        var heightMap = new byte[gw * gh];
        for (int hx = 0; hx < gw; hx++)
            for (int hy = 0; hy < gh; hy++)
                heightMap[hy * gw + hx] = v.HeightMap[hx, hy];

        var slope = new float[gw * gh];
        for (int sx = 0; sx < gw; sx++)
            for (int sy = 0; sy < gh; sy++)
                slope[sy * gw + sx] = v.Slope[sx, sy];

        var riverFlow = new int[gw * gh];
        for (int fx = 0; fx < gw; fx++)
            for (int fy = 0; fy < gh; fy++)
                riverFlow[fy * gw + fx] = v.RiverFlow[fx, fy];

        var surfaceWater = new float[gw * gh];
        for (int sx = 0; sx < gw; sx++)
            for (int sy = 0; sy < gh; sy++)
                surfaceWater[sy * gw + sx] = v.SurfaceWaterGrid[sx, sy];

        var groundwater = new float[gw * gh];
        for (int gx = 0; gx < gw; gx++)
            for (int gy = 0; gy < gh; gy++)
                groundwater[gy * gw + gx] = v.GroundwaterGrid[gx, gy];

        var nutrient = new float[gw * gh];
        for (int nx = 0; nx < gw; nx++)
            for (int ny = 0; ny < gh; ny++)
                nutrient[ny * gw + nx] = v.NutrientGrid[nx, ny];

        var permeability = new float[gw * gh];
        for (int px = 0; px < gw; px++)
            for (int py = 0; py < gh; py++)
                permeability[py * gw + px] = v.Permeability[px, py];

        var pressure = new float[gw * gh];
        for (int px = 0; px < gw; px++)
            for (int py = 0; py < gh; py++)
                pressure[py * gw + px] = v.Pressure[px, py];

        var windX = new float[gw * gh];
        var windY = new float[gw * gh];
        for (int wx = 0; wx < gw; wx++)
            for (int wy = 0; wy < gh; wy++)
            {
                windX[wy * gw + wx] = v.WindX[wx, wy];
                windY[wy * gw + wx] = v.WindY[wx, wy];
            }

        float totalEnergySrc = _engine.GenFoodEnergy + _engine.GenCorpseEnergy;
        float foodPct = totalEnergySrc > 0 ? _engine.GenFoodEnergy / totalEnergySrc * 100f : 0;
        float corpsePct = totalEnergySrc > 0 ? _engine.GenCorpseEnergy / totalEnergySrc * 100f : 0;

        var stats = new
        {
            attack_rate = _engine.GenTotalTicks > 0 ? (float)_engine.GenAttacks / _engine.GenTotalTicks : 0f,
            food_eaten = _engine.GenFoodEaten,
            corpses_eaten = _engine.GenCorpsesEaten,
            energy_source = new
            {
                food_pct = foodPct,
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
            plant_count = foodSnap.Length,
            total_energy = _engine.TotalEnergyInWorld,
            tick_exceptions = _engine.TickExceptionCount,
            agents,
            food,
            corpses,
            river,
            scent,
            food_scent = foodScent,
            temperature_grid = tempGrid,
            chemical_field = chemicalField,
            height_map = heightMap,
            slope,
            river_flow = riverFlow,
            surface_water = surfaceWater,
            groundwater,
            nutrient,
            permeability,
            pressure,
            wind_x = windX,
            wind_y = windY,
            water_cycle = new
            {
                humidity = _engine.Humidity,
                season_progress = _engine.SeasonProgress,
                is_wet_season = _engine.IsWetSeason
            },
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

internal record SpeedPayload([property: System.Text.Json.Serialization.JsonPropertyName("multiplier")] int Multiplier);
