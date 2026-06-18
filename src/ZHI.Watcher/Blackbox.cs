using Microsoft.Data.Sqlite;

namespace ZHI.Watcher;

public class Blackbox : IDisposable
{
    private readonly SqliteConnection _db;

    public Blackbox(string dbPath = "blackbox.db")
    {
        _db = new SqliteConnection($"Data Source={dbPath};Default Timeout=10");
        _db.Open();
        using var pragma = _db.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL";
        pragma.ExecuteNonQuery();
        InitSchema();
    }

    private void InitSchema()
    {
        // Check if this is a fresh DB or needs migration
        bool hasGenerationsTable = false;
        bool hasOldCpuColumn = false;

        using (var cmd = _db.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='generations'";
            hasGenerationsTable = cmd.ExecuteScalar() != null;
        }

        if (!hasGenerationsTable)
        {
            // Check if deaths table exists with old schema
            using (var cmd = _db.CreateCommand())
            {
                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='deaths'";
                if (cmd.ExecuteScalar() != null)
                {
                    // Check for old column name
                    using var pragma = _db.CreateCommand();
                    pragma.CommandText = "PRAGMA table_info(deaths)";
                    using var reader = pragma.ExecuteReader();
                    while (reader.Read())
                    {
                        if (reader.GetString(1) == "cpu_at_death") hasOldCpuColumn = true;
                    }
                }
            }
        }

        using var migrateCmd = _db.CreateCommand();

        if (hasOldCpuColumn)
        {
            migrateCmd.CommandText = """
                ALTER TABLE deaths RENAME COLUMN cpu_at_death TO stress_at_death;
                ALTER TABLE deaths DROP COLUMN mem_at_death;
                """;
            migrateCmd.ExecuteNonQuery();
        }

        // Create tables with correct schema
        migrateCmd.CommandText = """
            CREATE TABLE IF NOT EXISTS deaths (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                generation INTEGER NOT NULL,
                cause TEXT NOT NULL,
                stress_at_death REAL NOT NULL,
                existence_at_death REAL NOT NULL,
                hunger_at_death REAL NOT NULL DEFAULT 0,
                thirst_at_death REAL NOT NULL DEFAULT 0,
                temperature REAL NOT NULL DEFAULT 20,
                time_of_day REAL NOT NULL DEFAULT 12,
                pos_x INTEGER NOT NULL DEFAULT 0,
                pos_y INTEGER NOT NULL DEFAULT 0,
                attack_count INTEGER NOT NULL DEFAULT 0,
                eat_count INTEGER NOT NULL DEFAULT 0,
                signal_count INTEGER NOT NULL DEFAULT 0,
                respawn_count INTEGER NOT NULL DEFAULT 0,
                pre_death_states TEXT NOT NULL DEFAULT '{}',
                death_time TEXT NOT NULL,
                alive_seconds REAL NOT NULL
            );

            CREATE TABLE IF NOT EXISTS weights (
                id INTEGER PRIMARY KEY CHECK (id = 1),
                generation INTEGER NOT NULL,
                weights_bin BLOB,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS agent_weights (
                generation INTEGER NOT NULL,
                agent_id INTEGER NOT NULL,
                weights_bin BLOB,
                updated_at TEXT NOT NULL,
                PRIMARY KEY (generation, agent_id)
            );

            CREATE TABLE IF NOT EXISTS generations (
                generation INTEGER PRIMARY KEY,
                agent_count INTEGER NOT NULL,
                total_ticks INTEGER NOT NULL,
                attacks INTEGER NOT NULL,
                food_eaten INTEGER NOT NULL,
                bigfood_eaten INTEGER NOT NULL,
                corpses_eaten INTEGER NOT NULL,
                best_fitness REAL NOT NULL,
                best_alive_seconds REAL NOT NULL,
                avg_alive_seconds REAL NOT NULL,
                started_at TEXT NOT NULL,
                ended_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_deaths_gen ON deaths(generation);
            """;
        migrateCmd.ExecuteNonQuery();

        // Migrate existing DBs: add any missing columns
        var newColumns = new (string Name, string Type)[]
        {
            ("hunger_at_death", "REAL NOT NULL DEFAULT 0"),
            ("thirst_at_death", "REAL NOT NULL DEFAULT 0"),
            ("temperature", "REAL NOT NULL DEFAULT 20"),
            ("time_of_day", "REAL NOT NULL DEFAULT 12"),
            ("pos_x", "INTEGER NOT NULL DEFAULT 0"),
            ("pos_y", "INTEGER NOT NULL DEFAULT 0"),
            ("attack_count", "INTEGER NOT NULL DEFAULT 0"),
            ("eat_count", "INTEGER NOT NULL DEFAULT 0"),
            ("signal_count", "INTEGER NOT NULL DEFAULT 0"),
            ("respawn_count", "INTEGER NOT NULL DEFAULT 0"),
        };

        foreach (var col in newColumns)
        {
            try
            {
                using var addCmd = _db.CreateCommand();
                addCmd.CommandText = $"ALTER TABLE deaths ADD COLUMN {col.Name} {col.Type}";
                addCmd.ExecuteNonQuery();
            }
            catch (SqliteException) { /* column already exists */ }
        }
    }

    public void RecordDeath(DeathRecord record)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO deaths (generation, cause, stress_at_death, existence_at_death,
                hunger_at_death, thirst_at_death, temperature, time_of_day,
                pos_x, pos_y, attack_count, eat_count, signal_count, respawn_count,
                pre_death_states, death_time, alive_seconds)
            VALUES (@gen, @cause, @stress, @exist,
                @hunger, @thirst, @temp, @tod,
                @px, @py, @atk, @eat, @sig, @resp,
                @states, @time, @alive)
            """;
        cmd.Parameters.AddWithValue("@gen", record.Generation);
        cmd.Parameters.AddWithValue("@cause", record.Cause);
        cmd.Parameters.AddWithValue("@stress", record.StressAtDeath);
        cmd.Parameters.AddWithValue("@exist", record.ExistenceAtDeath);
        cmd.Parameters.AddWithValue("@hunger", record.HungerAtDeath);
        cmd.Parameters.AddWithValue("@thirst", record.ThirstAtDeath);
        cmd.Parameters.AddWithValue("@temp", record.Temperature);
        cmd.Parameters.AddWithValue("@tod", record.TimeOfDay);
        cmd.Parameters.AddWithValue("@px", record.PosX);
        cmd.Parameters.AddWithValue("@py", record.PosY);
        cmd.Parameters.AddWithValue("@atk", record.AttackCount);
        cmd.Parameters.AddWithValue("@eat", record.EatCount);
        cmd.Parameters.AddWithValue("@sig", record.SignalCount);
        cmd.Parameters.AddWithValue("@resp", record.RespawnCount);
        cmd.Parameters.AddWithValue("@states", record.PreDeathStatesJson);
        cmd.Parameters.AddWithValue("@time", record.DeathTime.ToString("O"));
        cmd.Parameters.AddWithValue("@alive", record.AliveSeconds);
        cmd.ExecuteNonQuery();
    }

    public void SaveGeneration(GenerationRecord record)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO generations (generation, agent_count, total_ticks, attacks, food_eaten, bigfood_eaten, corpses_eaten, best_fitness, best_alive_seconds, avg_alive_seconds, started_at, ended_at)
            VALUES (@gen, @agents, @ticks, @attacks, @food, @bigfood, @corpses, @bestFit, @bestAlive, @avgAlive, @started, @ended)
            """;
        cmd.Parameters.AddWithValue("@gen", record.Generation);
        cmd.Parameters.AddWithValue("@agents", record.AgentCount);
        cmd.Parameters.AddWithValue("@ticks", record.TotalTicks);
        cmd.Parameters.AddWithValue("@attacks", record.Attacks);
        cmd.Parameters.AddWithValue("@food", record.FoodEaten);
        cmd.Parameters.AddWithValue("@bigfood", record.BigFoodEaten);
        cmd.Parameters.AddWithValue("@corpses", record.CorpsesEaten);
        cmd.Parameters.AddWithValue("@bestFit", record.BestFitness);
        cmd.Parameters.AddWithValue("@bestAlive", record.BestAliveSeconds);
        cmd.Parameters.AddWithValue("@avgAlive", record.AvgAliveSeconds);
        cmd.Parameters.AddWithValue("@started", record.StartedAt);
        cmd.Parameters.AddWithValue("@ended", record.EndedAt);
        cmd.ExecuteNonQuery();
    }

    public GenerationRecord? GetLatestGeneration()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT * FROM generations ORDER BY generation DESC LIMIT 1";
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new GenerationRecord
        {
            Generation = reader.GetInt32(0),
            AgentCount = reader.GetInt32(1),
            TotalTicks = reader.GetInt32(2),
            Attacks = reader.GetInt32(3),
            FoodEaten = reader.GetInt32(4),
            BigFoodEaten = reader.GetInt32(5),
            CorpsesEaten = reader.GetInt32(6),
            BestFitness = reader.GetFloat(7),
            BestAliveSeconds = reader.GetFloat(8),
            AvgAliveSeconds = reader.GetFloat(9),
            StartedAt = reader.GetString(10),
            EndedAt = reader.GetString(11)
        };
    }

    public void CleanupOldData(int keepGenerations = 10, int keepDeaths = 10000)
    {
        using var cmd = _db.CreateCommand();
        // Clean old agent_weights
        cmd.CommandText = """
            DELETE FROM agent_weights WHERE generation < (SELECT COALESCE(MAX(generation), 0) - @keep FROM generations)
            """;
        cmd.Parameters.AddWithValue("@keep", keepGenerations);
        cmd.ExecuteNonQuery();

        // Clean old deaths (keep latest N)
        using var cmd2 = _db.CreateCommand();
        cmd2.CommandText = """
            DELETE FROM deaths WHERE id NOT IN (SELECT id FROM deaths ORDER BY id DESC LIMIT @keep)
            """;
        cmd2.Parameters.AddWithValue("@keep", keepDeaths);
        cmd2.ExecuteNonQuery();
    }

    public void SaveWeights(int generation, byte[] weights)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO weights (id, generation, weights_bin, updated_at)
            VALUES (1, @gen, @weights, @time)
            """;
        cmd.Parameters.AddWithValue("@gen", generation);
        cmd.Parameters.AddWithValue("@weights", weights);
        cmd.Parameters.AddWithValue("@time", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public byte[]? LoadWeights()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT weights_bin FROM weights WHERE id = 1";
        var result = cmd.ExecuteScalar();
        return result as byte[];
    }

    public void SaveAgentWeights(int generation, int agentId, byte[] weights)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO agent_weights (generation, agent_id, weights_bin, updated_at)
            VALUES (@gen, @agentId, @weights, @time)
            """;
        cmd.Parameters.AddWithValue("@gen", generation);
        cmd.Parameters.AddWithValue("@agentId", agentId);
        cmd.Parameters.AddWithValue("@weights", weights);
        cmd.Parameters.AddWithValue("@time", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public byte[]? LoadAgentWeights(int generation, int agentId)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT weights_bin FROM agent_weights WHERE generation = @gen AND agent_id = @agentId ORDER BY updated_at DESC LIMIT 1";
        cmd.Parameters.AddWithValue("@gen", generation);
        cmd.Parameters.AddWithValue("@agentId", agentId);
        return cmd.ExecuteScalar() as byte[];
    }

    public List<DeathRecord> GetRecentDeaths(int count = 10)
    {
        var records = new List<DeathRecord>();
        using var cmd = _db.CreateCommand();
        cmd.CommandText = $"SELECT * FROM deaths ORDER BY id DESC LIMIT {count}";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            records.Add(ReadDeathRecord(reader));
        }
        return records;
    }

    public int GetTotalDeaths()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM deaths";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public List<DeathRecord> GetAllDeaths()
    {
        var records = new List<DeathRecord>();
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT * FROM deaths ORDER BY id ASC";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            records.Add(ReadDeathRecord(reader));
        }
        return records;
    }

    private static DeathRecord ReadDeathRecord(SqliteDataReader reader)
    {
        return new DeathRecord
        {
            Id = reader.GetInt32(0),
            Generation = reader.GetInt32(1),
            Cause = reader.GetString(2),
            StressAtDeath = reader.GetFloat(3),
            ExistenceAtDeath = reader.GetFloat(4),
            HungerAtDeath = reader.GetFloat(5),
            ThirstAtDeath = reader.GetFloat(6),
            Temperature = reader.GetFloat(7),
            TimeOfDay = reader.GetFloat(8),
            PosX = reader.GetInt32(9),
            PosY = reader.GetInt32(10),
            AttackCount = reader.GetInt32(11),
            EatCount = reader.GetInt32(12),
            SignalCount = reader.GetInt32(13),
            RespawnCount = reader.GetInt32(14),
            PreDeathStatesJson = reader.GetString(15),
            DeathTime = DateTime.Parse(reader.GetString(16)),
            AliveSeconds = reader.GetDouble(17)
        };
    }

    public StatsResult GetStats()
    {
        var all = GetAllDeaths();
        var result = new StatsResult
        {
            TotalGenerations = all.Count > 0 ? all.Max(d => d.Generation) : 0,
            TotalDeaths = all.Count
        };

        if (all.Count == 0) return result;

        // Overall stats
        var suicides = all.Count(d => d.Cause == "SELF_TERMINATION");
        result.SuicideRateAll = all.Count > 0 ? (float)suicides / all.Count : 0;
        result.AvgAliveSecondsAll = all.Average(d => d.AliveSeconds);

        // Recent 10
        var recent = all.TakeLast(10).ToList();
        var recentSuicides = recent.Count(d => d.Cause == "SELF_TERMINATION");
        result.SuicideRateRecent_10 = recent.Count > 0 ? (float)recentSuicides / recent.Count : 0;
        result.AvgAliveSecondsRecent_10 = recent.Average(d => d.AliveSeconds);

        // Cause distribution
        result.CauseDistribution = all.GroupBy(d => d.Cause)
            .ToDictionary(g => g.Key, g => g.Count());

        // Aggregate stats from new dimensions
        result.AvgHungerAtDeath = all.Average(d => d.HungerAtDeath);
        result.AvgThirstAtDeath = all.Average(d => d.ThirstAtDeath);
        result.AvgTemperatureAtDeath = all.Average(d => d.Temperature);
        result.AvgAttacksPerLife = all.Average(d => d.AttackCount);
        result.AvgEatsPerLife = all.Average(d => d.EatCount);
        result.AvgSignalsPerLife = all.Average(d => d.SignalCount);

        // Night death rate (time 0-6 or 20-24 = night)
        var nightDeaths = all.Count(d => d.TimeOfDay < 6f || d.TimeOfDay >= 20f);
        result.NightDeathRate = all.Count > 0 ? (float)nightDeaths / all.Count : 0f;

        // Per-generation data
        result.Generations = all.Select(d => new GenerationStat
        {
            Generation = d.Generation,
            Cause = d.Cause,
            AliveSeconds = d.AliveSeconds
        }).ToList();

        return result;
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}
