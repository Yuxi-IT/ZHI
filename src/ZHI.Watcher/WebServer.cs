using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ZHI.Shared;

namespace ZHI.Watcher;

public partial class WebServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly List<WebSocket> _stateClients = new();
    private readonly List<WebSocket> _logClients = new();
    private readonly Lock _stateClientsLock = new();
    private readonly Lock _logClientsLock = new();
    private readonly WorldManager _worldManager;
    private readonly int _port;
    private readonly List<string> _logBuffer = new();
    private readonly Lock _logLock = new();
    private const int MaxLogBuffer = 500;

    private CosmosEngine? _engine;
    private Blackbox? _blackbox;
    private Task _engineTask = Task.CompletedTask;
    private CancellationTokenSource _engineCts = new();
    private string? _currentWorldName;

    public CosmosEngine? CurrentEngine => _engine;

    public WebServer(WorldManager worldManager, int httpPort = 8088)
    {
        _worldManager = worldManager;
        _port = httpPort;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{httpPort}/");
    }

    private void OnEngineLog(string message)
    {
        lock (_logLock)
        {
            _logBuffer.Add(message);
            if (_logBuffer.Count > MaxLogBuffer)
                _logBuffer.RemoveAt(0);
        }

        var payload = JsonSerializer.Serialize(new
        {
            type = "log",
            time = DateTime.Now.ToString("HH:mm:ss"),
            message
        });
        _ = BroadcastToLogClientsAsync(payload);
    }

    public async Task StartAsync(CancellationToken ct)
    {
        Task broadcastTask = Task.CompletedTask;
        try
        {
            _listener.Start();
            Console.WriteLine($"[WebServer] http://localhost:{_port}");

            broadcastTask = BroadcastLoop(ct);

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync().WaitAsync(ct);
                    _ = Task.Run(() => HandleRequest(context, ct), ct);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WebServer] Accept error: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WebServer] Fatal: {ex.Message}");
            throw;
        }

        await broadcastTask;
    }

    public async Task StartWorldAsync(string name)
    {
        if (_engine != null)
            throw new InvalidOperationException("A world is already running. Stop it first.");

        var (config, meta, dbPath) = _worldManager.LoadWorld(name);

        _blackbox = new Blackbox(dbPath);
        _engine = new CosmosEngine(config, _blackbox, dbPath, _worldManager.GetWorldDir(name));
        _engine.OnLog += OnEngineLog;
        _currentWorldName = name;

        meta.Status = "running";
        meta.LastRunAt = DateTime.UtcNow.ToString("O");
        _worldManager.SaveWorldMeta(name, meta);

        _engineCts = new CancellationTokenSource();
        _engineTask = Task.Run(async () =>
        {
            try
            {
                await _engine.RunAsync();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebServer] Engine fault: {ex.Message}");
                OnEngineLog($"[System] Engine fault: {ex.Message}");
            }
        });

        _ = BroadcastStatusAsync("running");
        Console.WriteLine($"[WebServer] World '{name}' started.");
    }

    private async Task StopWorldAsync()
    {
        if (_engine == null) return;

        var name = _currentWorldName;
        _engine.GracefulStop();

        try { await _engineTask.WaitAsync(TimeSpan.FromSeconds(10)); }
        catch (TimeoutException) { Console.WriteLine("[WebServer] Engine stop timed out."); }
        catch (Exception ex) { Console.WriteLine($"[WebServer] Engine stop error: {ex.Message}"); }

        int gen = _engine.Generation;
        int deaths = _engine.TotalDeaths;

        if (_engine is IDisposable d) d.Dispose();
        _blackbox?.Dispose();
        _engine = null;
        _blackbox = null;
        _currentWorldName = null;
        _engineCts.Dispose();

        if (name != null)
        {
            try
            {
                var (_, meta, _) = _worldManager.LoadWorld(name);
                meta.Status = "stopped";
                meta.LastRunAt = DateTime.UtcNow.ToString("O");
                meta.TotalGenerations = gen;
                meta.TotalDeaths = deaths;
                _worldManager.SaveWorldMeta(name, meta);
            }
            catch (Exception ex) { Console.WriteLine($"[WebServer] Failed to save world meta on stop: {ex.Message}"); }
        }

        _ = BroadcastStatusAsync("stopped");
        Console.WriteLine($"[WebServer] World '{name}' stopped.");
    }

    private async Task BroadcastStatusAsync(string status)
    {
        var payload = JsonSerializer.Serialize(new { type = "status", status });
        await BroadcastToStateClientsAsync(payload);
    }

    private async Task HandleRequest(HttpListenerContext context, CancellationToken ct)
    {
        var path = context.Request.Url?.AbsolutePath ?? "/";

        if (context.Request.IsWebSocketRequest)
        {
            bool isLogChannel = path == "/ws/logs";
            await HandleWebSocket(context, ct, isLogChannel);
            return;
        }

        context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
        context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

        if (context.Request.HttpMethod == "OPTIONS")
        {
            context.Response.StatusCode = 204;
            context.Response.Close();
            return;
        }

        // World management
        if (path == "/api/worlds" && context.Request.HttpMethod == "GET")
        {
            await ServeWorlds(context);
            return;
        }
        if (path == "/api/world/create" && context.Request.HttpMethod == "POST")
        {
            await HandleWorldCreate(context);
            return;
        }
        if (path == "/api/world/start" && context.Request.HttpMethod == "POST")
        {
            await HandleWorldStart(context);
            return;
        }
        if (path == "/api/world/delete" && context.Request.HttpMethod == "POST")
        {
            await HandleWorldDelete(context);
            return;
        }
        if (path == "/api/world/clone" && context.Request.HttpMethod == "POST")
        {
            await HandleWorldClone(context);
            return;
        }

        // Engine controls
        if (path == "/api/pause" && context.Request.HttpMethod == "POST")
        {
            await HandlePause(context);
            return;
        }
        if (path == "/api/stop" && context.Request.HttpMethod == "POST")
        {
            await HandleStop(context);
            return;
        }
        if (path == "/api/speed" && context.Request.HttpMethod == "POST")
        {
            await HandleSetSpeed(context);
            return;
        }

        // Data endpoints (require engine)
        if (path == "/api/stats")
        {
            await ServeStats(context);
            return;
        }
        if (path == "/api/cosmos")
        {
            await ServeCosmosState(context);
            return;
        }
        if (path == "/api/config" && context.Request.HttpMethod == "GET")
        {
            await ServeConfig(context);
            return;
        }
        if (path == "/api/config" && context.Request.HttpMethod == "POST")
        {
            await HandleConfigSave(context);
            return;
        }

        // Static files
        if (path == "/" || path == "/index.html")
        {
            await ServeStaticFile(context, "index.html", "text/html; charset=utf-8");
            return;
        }
        if (path.StartsWith("/assets/") || path.EndsWith(".js") || path.EndsWith(".css") || path.EndsWith(".svg") || path.EndsWith(".ico"))
        {
            var filePath = path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            await ServeStaticFile(context, filePath, GetContentType(path));
            return;
        }
        if (!path.Contains('.'))
        {
            await ServeStaticFile(context, "index.html", "text/html; charset=utf-8");
            return;
        }

        context.Response.StatusCode = 404;
        context.Response.Close();
    }

    // === World endpoints ===

    private async Task ServeWorlds(HttpListenerContext context)
    {
        var worlds = _worldManager.ListWorlds();
        var json = JsonSerializer.Serialize(worlds, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
        var bytes = Encoding.UTF8.GetBytes(json);
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }

    private async Task HandleWorldCreate(HttpListenerContext context)
    {
        using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
        var body = await reader.ReadToEndAsync();
        var req = JsonSerializer.Deserialize<JsonElement>(body);

        var name = req.TryGetProperty("name", out var n) ? n.GetString() ?? "untitled" : "untitled";
        int? seed = req.TryGetProperty("seed", out var s) && s.ValueKind != JsonValueKind.Null ? s.GetInt32() : null;
        var desc = req.TryGetProperty("description", out var d) ? d.GetString() : "";
        ZhiConfig config;
        if (req.TryGetProperty("config", out var cfgEl))
            config = JsonSerializer.Deserialize<ZhiConfig>(cfgEl.GetRawText(), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }) ?? new ZhiConfig();
        else
            config = new ZhiConfig();

        try
        {
            var meta = _worldManager.CreateWorld(name, seed, desc, config);
            var json = JsonSerializer.Serialize(meta, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
            var bytes = Encoding.UTF8.GetBytes(json);
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes);
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 400;
            var err = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { error = ex.Message }));
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentLength64 = err.Length;
            await context.Response.OutputStream.WriteAsync(err);
        }
        context.Response.Close();
    }

    private async Task HandleWorldStart(HttpListenerContext context)
    {
        using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
        var body = await reader.ReadToEndAsync();
        var req = JsonSerializer.Deserialize<JsonElement>(body);
        var name = req.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";

        try
        {
            await StartWorldAsync(name);
            var resp = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { ok = true, name }));
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentLength64 = resp.Length;
            await context.Response.OutputStream.WriteAsync(resp);
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 400;
            var err = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { error = ex.Message }));
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentLength64 = err.Length;
            await context.Response.OutputStream.WriteAsync(err);
        }
        context.Response.Close();
    }

    private async Task HandleWorldDelete(HttpListenerContext context)
    {
        using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
        var body = await reader.ReadToEndAsync();
        var req = JsonSerializer.Deserialize<JsonElement>(body);
        var name = req.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";

        if (name == _currentWorldName)
            await StopWorldAsync();

        _worldManager.DeleteWorld(name);
        var resp = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { ok = true }));
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength64 = resp.Length;
        await context.Response.OutputStream.WriteAsync(resp);
        context.Response.Close();
    }

    private async Task HandleWorldClone(HttpListenerContext context)
    {
        using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
        var body = await reader.ReadToEndAsync();
        var req = JsonSerializer.Deserialize<JsonElement>(body);
        var sourceName = req.TryGetProperty("source", out var src) ? src.GetString() ?? "" : "";
        var newName = req.TryGetProperty("name", out var nn) ? nn.GetString() ?? "clone" : "clone";
        int? newSeed = req.TryGetProperty("seed", out var ns) && ns.ValueKind != JsonValueKind.Null ? ns.GetInt32() : null;

        try
        {
            var meta = _worldManager.CloneWorld(sourceName, newName, newSeed);
            var json = JsonSerializer.Serialize(meta, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
            var bytes = Encoding.UTF8.GetBytes(json);
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes);
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 400;
            var err = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { error = ex.Message }));
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentLength64 = err.Length;
            await context.Response.OutputStream.WriteAsync(err);
        }
        context.Response.Close();
    }

    // === Engine control endpoints ===

    private async Task HandlePause(HttpListenerContext context)
    {
        _engine?.TogglePause();
        var status = _engine?.Paused == true ? "paused" : "running";
        var resp = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { ok = true, status }));
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength64 = resp.Length;
        await context.Response.OutputStream.WriteAsync(resp);
        context.Response.Close();
        _ = BroadcastStatusAsync(status);
    }

    private async Task HandleStop(HttpListenerContext context)
    {
        await StopWorldAsync();
        var resp = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { ok = true }));
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength64 = resp.Length;
        await context.Response.OutputStream.WriteAsync(resp);
        context.Response.Close();
    }

    private async Task HandleSetSpeed(HttpListenerContext context)
    {
        using var reader = new StreamReader(context.Request.InputStream);
        var body = await reader.ReadToEndAsync();
        var req = JsonSerializer.Deserialize<SpeedPayload>(body);
        _engine?.SetSpeed(req?.Multiplier ?? 1);
        var resp = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { ok = true, speed = _engine?.SpeedMultiplier ?? 1 }));
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength64 = resp.Length;
        await context.Response.OutputStream.WriteAsync(resp);
        context.Response.Close();
    }

    // === Data endpoints ===

    private async Task ServeStats(HttpListenerContext context)
    {
        var stats = _blackbox?.GetStats() ?? new StatsResult();
        var json = JsonSerializer.Serialize(stats, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        });
        var bytes = Encoding.UTF8.GetBytes(json);
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }

    private async Task ServeCosmosState(HttpListenerContext context)
    {
        var json = BuildCosmosPayload();
        var bytes = Encoding.UTF8.GetBytes(json);
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }

    private async Task ServeConfig(HttpListenerContext context)
    {
        if (_engine == null)
        {
            context.Response.StatusCode = 404;
            context.Response.Close();
            return;
        }
        var json = JsonSerializer.Serialize(_engine.CurrentConfig, new JsonSerializerOptions { WriteIndented = true });
        var bytes = Encoding.UTF8.GetBytes(json);
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }

    private async Task HandleConfigSave(HttpListenerContext context)
    {
        if (_engine == null)
        {
            context.Response.StatusCode = 404;
            context.Response.Close();
            return;
        }
        using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
        var body = await reader.ReadToEndAsync();
        var newConfig = JsonSerializer.Deserialize<ZhiConfig>(body);
        if (newConfig == null)
        {
            context.Response.StatusCode = 400;
            context.Response.Close();
            return;
        }

        _engine.UpdateConfigAndRestart(newConfig);
        if (_currentWorldName != null)
        {
            try
            {
                var (_, meta, _) = _worldManager.LoadWorld(_currentWorldName);
                meta.Config = newConfig;
                meta.Status = "running";
                _worldManager.SaveWorldMeta(_currentWorldName, meta);
            }
            catch (Exception ex) { Console.WriteLine($"[WebServer] Failed to update world meta after config save: {ex.Message}"); }
        }

        var resp = JsonSerializer.Serialize(new { ok = true });
        var bytes = Encoding.UTF8.GetBytes(resp);
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }

    private async Task ServeStaticFile(HttpListenerContext context, string relativePath, string contentType)
    {
        // Normalize and validate path to prevent directory traversal
        var wwwrootDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "wwwroot"));
        var resolvedPath = Path.GetFullPath(Path.Combine(wwwrootDir, relativePath));
        if (!resolvedPath.StartsWith(wwwrootDir + Path.DirectorySeparatorChar) && resolvedPath != wwwrootDir)
        {
            context.Response.StatusCode = 403;
            context.Response.ContentType = "text/plain; charset=utf-8";
            var forbid = Encoding.UTF8.GetBytes("Forbidden");
            context.Response.ContentLength64 = forbid.Length;
            await context.Response.OutputStream.WriteAsync(forbid);
            context.Response.Close();
            return;
        }

        if (!File.Exists(resolvedPath))
        {
            // Fallback: try relative to project root (for dev convenience)
            var devDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "wwwroot"));
            var devPath = Path.GetFullPath(Path.Combine(devDir, relativePath));
            if (devPath.StartsWith(devDir + Path.DirectorySeparatorChar) && File.Exists(devPath))
                resolvedPath = devPath;
            else
            {
                context.Response.StatusCode = 404;
                context.Response.ContentType = "text/plain; charset=utf-8";
                var msg = Encoding.UTF8.GetBytes($"{relativePath} not found");
                context.Response.ContentLength64 = msg.Length;
                await context.Response.OutputStream.WriteAsync(msg);
                context.Response.Close();
                return;
            }
        }

        var bytes = await File.ReadAllBytesAsync(resolvedPath);
        context.Response.ContentType = contentType;
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }

    private static string GetContentType(string path) => Path.GetExtension(path) switch
    {
        ".js" => "application/javascript; charset=utf-8",
        ".css" => "text/css; charset=utf-8",
        ".svg" => "image/svg+xml",
        ".ico" => "image/x-icon",
        ".png" => "image/png",
        ".json" => "application/json; charset=utf-8",
        ".woff2" => "font/woff2",
        ".woff" => "font/woff",
        _ => "application/octet-stream"
    };

    public void Dispose()
    {
        try { _engineCts.Cancel(); } catch (Exception ex) { Console.WriteLine($"[WebServer] Dispose cts error: {ex.Message}"); }
        try { _engine?.Dispose(); } catch (Exception ex) { Console.WriteLine($"[WebServer] Dispose engine error: {ex.Message}"); }
        try { _blackbox?.Dispose(); } catch (Exception ex) { Console.WriteLine($"[WebServer] Dispose blackbox error: {ex.Message}"); }
        try { _listener.Stop(); } catch (Exception ex) { Console.WriteLine($"[WebServer] Dispose listener error: {ex.Message}"); }

        lock (_stateClientsLock)
        {
            foreach (var ws in _stateClients) try { ws.Dispose(); } catch (Exception ex) { Console.WriteLine($"[WebServer] Dispose ws error: {ex.Message}"); }
            _stateClients.Clear();
        }
        lock (_logClientsLock)
        {
            foreach (var ws in _logClients) try { ws.Dispose(); } catch (Exception ex) { Console.WriteLine($"[WebServer] Dispose ws error: {ex.Message}"); }
            _logClients.Clear();
        }
    }
}
