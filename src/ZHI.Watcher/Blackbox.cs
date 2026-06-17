using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace ZHI.Watcher;

public class DeathRecord
{
    public int Id { get; set; }
    public int Generation { get; set; }
    public string Cause { get; set; } = "";
    public float CpuAtDeath { get; set; }
    public float MemAtDeath { get; set; }
    public float ExistenceAtDeath { get; set; }
    public string PreDeathStatesJson { get; set; } = "[]";
    public DateTime DeathTime { get; set; }
    public double AliveSeconds { get; set; }
}

public class Blackbox : IDisposable
{
    private readonly SqliteConnection _db;

    public Blackbox(string dbPath = "blackbox.db")
    {
        _db = new SqliteConnection($"Data Source={dbPath}");
        _db.Open();
        InitSchema();
    }

    private void InitSchema()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS deaths (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                generation INTEGER NOT NULL,
                cause TEXT NOT NULL,
                cpu_at_death REAL NOT NULL,
                mem_at_death REAL NOT NULL,
                existence_at_death REAL NOT NULL,
                pre_death_states TEXT NOT NULL,
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
            """;
        cmd.ExecuteNonQuery();
    }

    public void RecordDeath(DeathRecord record)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO deaths (generation, cause, cpu_at_death, mem_at_death, existence_at_death, pre_death_states, death_time, alive_seconds)
            VALUES (@gen, @cause, @cpu, @mem, @exist, @states, @time, @alive)
            """;
        cmd.Parameters.AddWithValue("@gen", record.Generation);
        cmd.Parameters.AddWithValue("@cause", record.Cause);
        cmd.Parameters.AddWithValue("@cpu", record.CpuAtDeath);
        cmd.Parameters.AddWithValue("@mem", record.MemAtDeath);
        cmd.Parameters.AddWithValue("@exist", record.ExistenceAtDeath);
        cmd.Parameters.AddWithValue("@states", record.PreDeathStatesJson);
        cmd.Parameters.AddWithValue("@time", record.DeathTime.ToString("O"));
        cmd.Parameters.AddWithValue("@alive", record.AliveSeconds);
        cmd.ExecuteNonQuery();
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
            records.Add(new DeathRecord
            {
                Id = reader.GetInt32(0),
                Generation = reader.GetInt32(1),
                Cause = reader.GetString(2),
                CpuAtDeath = reader.GetFloat(3),
                MemAtDeath = reader.GetFloat(4),
                ExistenceAtDeath = reader.GetFloat(5),
                PreDeathStatesJson = reader.GetString(6),
                DeathTime = DateTime.Parse(reader.GetString(7)),
                AliveSeconds = reader.GetDouble(8)
            });
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
            records.Add(new DeathRecord
            {
                Id = reader.GetInt32(0),
                Generation = reader.GetInt32(1),
                Cause = reader.GetString(2),
                CpuAtDeath = reader.GetFloat(3),
                MemAtDeath = reader.GetFloat(4),
                ExistenceAtDeath = reader.GetFloat(5),
                PreDeathStatesJson = reader.GetString(6),
                DeathTime = DateTime.Parse(reader.GetString(7)),
                AliveSeconds = reader.GetDouble(8)
            });
        }
        return records;
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
        result.SuicideRateRecent10 = recent.Count > 0 ? (float)recentSuicides / recent.Count : 0;
        result.AvgAliveSecondsRecent10 = recent.Average(d => d.AliveSeconds);

        // Cause distribution
        result.CauseDistribution = all.GroupBy(d => d.Cause)
            .ToDictionary(g => g.Key, g => g.Count());

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

public class StatsResult
{
    public int TotalGenerations { get; set; }
    public int TotalDeaths { get; set; }
    public float SuicideRateAll { get; set; }
    public float SuicideRateRecent10 { get; set; }
    public double AvgAliveSecondsAll { get; set; }
    public double AvgAliveSecondsRecent10 { get; set; }
    public Dictionary<string, int> CauseDistribution { get; set; } = new();
    public List<GenerationStat> Generations { get; set; } = new();
}

public class GenerationStat
{
    public int Generation { get; set; }
    public string Cause { get; set; } = "";
    public double AliveSeconds { get; set; }
}
