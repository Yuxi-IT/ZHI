using System.Text.Json;
using ZHI.Shared;

namespace ZHI.Watcher;

public class WorldManager
{
    private readonly string _worldsDir;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public WorldManager(string baseDir)
    {
        _worldsDir = Path.Combine(baseDir, "worlds");
        Directory.CreateDirectory(_worldsDir);
    }

    public List<WorldMeta> ListWorlds()
    {
        var worlds = new List<WorldMeta>();
        if (!Directory.Exists(_worldsDir)) return worlds;

        foreach (var dir in Directory.GetDirectories(_worldsDir))
        {
            var jsonPath = Path.Combine(dir, "world.json");
            if (!File.Exists(jsonPath)) continue;

            try
            {
                var json = File.ReadAllText(jsonPath);
                var meta = JsonSerializer.Deserialize<WorldMeta>(json, JsonOpts);
                if (meta != null)
                {
                    meta.Name = Path.GetFileName(dir);
                    // Reset stale running status from previous backend session
                    if (meta.Status == "running")
                    {
                        meta.Status = "stopped";
                        SaveWorldMeta(meta.Name, meta);
                    }
                    worlds.Add(meta);
                }
            }
            catch { /* skip corrupt worlds */ }
        }

        return worlds.OrderByDescending(w => w.CreatedAt).ToList();
    }

    public WorldMeta CreateWorld(string name, int? seed, string? description, ZhiConfig config)
    {
        var worldDir = Path.Combine(_worldsDir, SanitizeName(name));
        if (Directory.Exists(worldDir))
            throw new InvalidOperationException($"World '{name}' already exists.");

        Directory.CreateDirectory(worldDir);

        config.Seed = seed;

        var meta = new WorldMeta
        {
            Name = name,
            Description = description ?? "",
            Seed = seed,
            CreatedAt = DateTime.UtcNow.ToString("O"),
            Status = "stopped",
            Config = config
        };

        SaveWorldMeta(name, meta);
        return meta;
    }

    public (ZhiConfig config, WorldMeta meta, string dbPath) LoadWorld(string name)
    {
        var worldDir = Path.Combine(_worldsDir, SanitizeName(name));
        var jsonPath = Path.Combine(worldDir, "world.json");

        if (!File.Exists(jsonPath))
            throw new FileNotFoundException($"World '{name}' not found.");

        var json = File.ReadAllText(jsonPath);
        var meta = JsonSerializer.Deserialize<WorldMeta>(json, JsonOpts)
            ?? throw new InvalidOperationException($"Failed to parse world.json for '{name}'.");

        if (meta.Config == null)
            throw new InvalidOperationException($"World '{name}' has no embedded config.");

        var dbPath = Path.Combine(worldDir, "blackbox.db");
        return (meta.Config, meta, dbPath);
    }

    public void SaveWorldMeta(string name, WorldMeta meta)
    {
        var worldDir = Path.Combine(_worldsDir, SanitizeName(name));
        Directory.CreateDirectory(worldDir);

        var jsonPath = Path.Combine(worldDir, "world.json");
        var json = JsonSerializer.Serialize(meta, JsonOpts);
        File.WriteAllText(jsonPath, json);
    }

    public void DeleteWorld(string name)
    {
        var worldDir = Path.Combine(_worldsDir, SanitizeName(name));
        if (Directory.Exists(worldDir))
            Directory.Delete(worldDir, recursive: true);
    }

    public WorldMeta CloneWorld(string sourceName, string newName, int? newSeed)
    {
        var (config, sourceMeta, _) = LoadWorld(sourceName);
        config.Seed = newSeed;
        return CreateWorld(newName, config.Seed, $"Cloned from {sourceName}", config);
    }

    private static string SanitizeName(string name)
    {
        var sanitized = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
        return string.IsNullOrWhiteSpace(sanitized) ? "untitled" : sanitized;
    }

    public string GetWorldDir(string name) => Path.Combine(_worldsDir, SanitizeName(name));
}
