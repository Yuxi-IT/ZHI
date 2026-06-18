using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ZHI.Shared;

namespace ZHI.Watcher;

public partial class WebServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly List<WebSocket> _stateClients = new();  // /ws — cosmos state
    private readonly List<WebSocket> _logClients = new();    // /ws/logs — logs + events
    private readonly Lock _stateClientsLock = new();
    private readonly Lock _logClientsLock = new();
    private readonly CosmosEngine _engine;
    private readonly Blackbox _blackbox;
    private readonly int _port;
    private readonly List<string> _logBuffer = new();
    private readonly Lock _logLock = new();
    private const int MaxLogBuffer = 500;

    public WebServer(CosmosEngine engine, Blackbox blackbox, int httpPort = 8088)
    {
        _engine = engine;
        _blackbox = blackbox;
        _port = httpPort;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{httpPort}/");

        _engine.OnLog += OnEngineLog;
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
            Console.WriteLine($"[WebServer] http://localhost:{_port} (listener started)");

            broadcastTask = BroadcastLoop(ct);

            Console.WriteLine("[WebServer] Entering accept loop...");
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
                    Console.WriteLine($"[WebServer] Error: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WebServer] Fatal: {ex.GetType().Name}: {ex.Message}");
            throw;
        }

        await broadcastTask;
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

        // CORS
        context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
        context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, OPTIONS");
        context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

        if (context.Request.HttpMethod == "OPTIONS")
        {
            context.Response.StatusCode = 204;
            context.Response.Close();
            return;
        }

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
            var contentType = GetContentType(path);
            await ServeStaticFile(context, filePath, contentType);
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

    private async Task ServeStaticFile(HttpListenerContext context, string relativePath, string contentType)
    {
        var filePath = Path.Combine(AppContext.BaseDirectory, "wwwroot", relativePath);
        if (!File.Exists(filePath))
        {
            var srcPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "wwwroot", relativePath);
            if (File.Exists(srcPath))
                filePath = srcPath;
        }

        if (!File.Exists(filePath))
        {
            context.Response.StatusCode = 404;
            var msg = Encoding.UTF8.GetBytes($"{relativePath} not found");
            await context.Response.OutputStream.WriteAsync(msg);
            context.Response.Close();
            return;
        }

        var bytes = await File.ReadAllBytesAsync(filePath);
        context.Response.ContentType = contentType;
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }

    private async Task ServeStats(HttpListenerContext context)
    {
        var stats = _blackbox.GetStats();
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
        var json = JsonSerializer.Serialize(_engine.CurrentConfig, new JsonSerializerOptions { WriteIndented = true });
        var bytes = Encoding.UTF8.GetBytes(json);
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }

    private async Task HandleConfigSave(HttpListenerContext context)
    {
        using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
        var body = await reader.ReadToEndAsync();
        var newConfig = JsonSerializer.Deserialize<ZhiConfig>(body);
        if (newConfig == null)
        {
            context.Response.StatusCode = 400;
            context.Response.Close();
            return;
        }

        bool restart = context.Request.QueryString["restart"] == "true";
        _engine.UpdateConfigAndRestart(newConfig);

        var resp = JsonSerializer.Serialize(new { ok = true, restarted = restart });
        var bytes = Encoding.UTF8.GetBytes(resp);
        context.Response.ContentType = "application/json; charset=utf-8";
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
        try { _listener.Stop(); } catch { }
        lock (_stateClientsLock)
        {
            foreach (var ws in _stateClients) try { ws.Dispose(); } catch { }
            _stateClients.Clear();
        }
        lock (_logClientsLock)
        {
            foreach (var ws in _logClients) try { ws.Dispose(); } catch { }
            _logClients.Clear();
        }
    }
}
