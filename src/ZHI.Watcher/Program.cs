using System.Text.Json;
using ZHI.Shared;

namespace ZHI.Watcher;

class Program
{
    static async Task Main(string[] args)
    {
        Console.Title = "ZHI · Cosmos";

        // Resolve base directory for worlds
        var baseDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..");
        if (!Directory.Exists(baseDir))
            baseDir = ".";

        // Legacy migration: move old config.json + blackbox.db into worlds/legacy/
        MigrateLegacyData(baseDir);

        var worldManager = new WorldManager(baseDir);

        // Check CLI args for direct world start
        string? autoStartWorld = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--world" && i + 1 < args.Length)
                autoStartWorld = args[i + 1];
        }

        var webServer = new WebServer(worldManager, httpPort: 8088);

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            webServer.CurrentEngine?.RequestShutdown();
            cts.Cancel();
        };

        Console.WriteLine($"[cosmos] Worlds dir: {worldManager.GetWorldDir(".")[..^1]}");
        Console.WriteLine($"[cosmos] Dashboard: http://localhost:8088");

        if (autoStartWorld != null)
        {
            try { await webServer.StartWorldAsync(autoStartWorld); }
            catch (Exception ex) { Console.WriteLine($"[cosmos] Failed to auto-start world: {ex.Message}"); }
        }

        try
        {
            await webServer.StartAsync(cts.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Console.WriteLine($"[cosmos] Error: {ex}");
        }
        finally
        {
            cts.Cancel();
            webServer.Dispose();
        }
    }

    private static void MigrateLegacyData(string baseDir)
    {
        var configPath = Path.Combine(baseDir, "config.json");
        var dbPath = Path.Combine(baseDir, "blackbox.db");
        var legacyDir = Path.Combine(baseDir, "worlds", "legacy");

        if (!File.Exists(configPath) || !File.Exists(dbPath))
            return;

        // Don't migrate if worlds already exist
        if (Directory.Exists(Path.Combine(baseDir, "worlds")) && Directory.GetFileSystemEntries(Path.Combine(baseDir, "worlds")).Length > 0)
            return;

        Console.WriteLine("[cosmos] Migrating legacy config.json + blackbox.db to worlds/legacy/...");

        try
        {
            Directory.CreateDirectory(legacyDir);

            var configJson = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<ZhiConfig>(configJson, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            }) ?? new ZhiConfig();

            var meta = new WorldMeta
            {
                Name = "legacy",
                Description = "Migrated from legacy config.json",
                Seed = config.Seed,
                CreatedAt = DateTime.UtcNow.ToString("O"),
                Status = "stopped",
                Config = config
            };

            var metaJson = JsonSerializer.Serialize(meta, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });
            File.WriteAllText(Path.Combine(legacyDir, "world.json"), metaJson);

            File.Move(dbPath, Path.Combine(legacyDir, "blackbox.db"));
            File.Delete(configPath);

            Console.WriteLine("[cosmos] Legacy data migrated successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[cosmos] Migration warning: {ex.Message}");
        }
    }
}
