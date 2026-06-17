using System.Text.Json;
using ZHI.Shared;

namespace ZHI.Watcher;

class Program
{
    static async Task Main(string[] args)
    {
        Console.Title = "ZHI · Cosmos";

        // 加载配置
        var configPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "config.json");
        if (!File.Exists(configPath))
            configPath = "config.json";
        if (!File.Exists(configPath))
        {
            Console.WriteLine("[cosmos] config.json not found, using defaults");
            configPath = null;
        }

        ZhiConfig config;
        if (configPath != null)
        {
            var json = await File.ReadAllTextAsync(configPath);
            config = JsonSerializer.Deserialize<ZhiConfig>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            }) ?? new ZhiConfig();

            // Detect if deserialization silently failed (all fields at default)
            if (config.Grid.Width == 0 || config.Cosmos.AgentCount == 0)
            {
                Console.WriteLine("[cosmos] WARNING: config.json appears corrupt or in wrong format — using defaults");
                Console.WriteLine("[cosmos] Check that config.json keys are snake_case (e.g. agent_count, grid_width)");
                config = new ZhiConfig();
            }
        }
        else
        {
            config = new ZhiConfig();
        }

        var actualConfigPath = configPath ?? "config.json";

        // 初始化黑匣子
        var blackboxDir = Path.GetDirectoryName(Path.GetFullPath(actualConfigPath)) ?? ".";
        var blackboxPath = Path.Combine(blackboxDir, "blackbox.db");
        var blackbox = new Blackbox(blackboxPath);

        // 初始化多智能体引擎
        var engine = new CosmosEngine(config, blackbox, actualConfigPath);

        // 启动 WebSocket 仪表盘
        var webServer = new WebServer(engine, blackbox, httpPort: 8088);

        // Ctrl+C 关闭
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            engine.RequestShutdown();
            cts.Cancel();
        };

        Console.WriteLine($"[cosmos] {config.Cosmos.AgentCount} agents, http://localhost:8088");

        // 并行运行引擎和 Web 服务
        var engineTask = engine.RunAsync();
        var webTask = webServer.StartAsync(cts.Token);

        try
        {
            var completed = await Task.WhenAny(engineTask, webTask);

            // Observe the completed task — this throws if it faulted
            try { await completed; }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Console.WriteLine($"[cosmos] Task fault: {ex}"); }

            cts.Cancel();

            // Wait for the remaining task
            var remaining = (completed == engineTask) ? webTask : engineTask;
            try { await remaining; }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Console.WriteLine($"[cosmos] Remaining task fault: {ex}"); }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[cosmos] Error: {ex}");
        }
        finally
        {
            cts.Cancel();
            webServer.Dispose();
            engine.Dispose();
        }
    }
}
