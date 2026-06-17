using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ZHI.Shared;

namespace ZHI.Watcher;

public class WebServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly List<WebSocket> _clients = new();
    private readonly Lock _clientsLock = new();
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
        _ = BroadcastAsync(payload);
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
            await HandleWebSocket(context, ct);
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

    private string BuildCosmosPayload()
    {
        var v = _engine.State;
        int n = _engine.AgentCount;
        var agents = new List<object>(n);
        for (int i = 0; i < n; i++)
        {
            // Snapshot SignalMemory for this agent
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
                thirst = v.Thirst[i],
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
                signal_count = v.SignalCount[i],
                facing_direction = v.FacingDirection[i],
                is_hiding = v.IsHiding[i]
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
            food.Add(new { x = f.X, y = f.Y, width = f.Width, height = f.Height, ttl = f.TTL, energy = f.Energy, is_big = f.IsBig });

        var corpses = new List<object>(corpseSnap.Length);
        foreach (var c in corpseSnap)
            corpses.Add(new { x = c.X, y = c.Y, ttl = c.TTL, energy = c.Energy });

        // Serialize river grid as flat array (0=land, 1=shallow, 2=deep)
        int gw = ToolDefinitions.GridWidth;
        int gh = ToolDefinitions.GridHeight;
        var river = new int[gw * gh];
        for (int rx = 0; rx < gw; rx++)
            for (int ry = 0; ry < gh; ry++)
                river[ry * gw + rx] = v.RiverGrid[rx, ry];

        // Compute energy source percentages
        float totalEnergySrc = _engine.GenFoodEnergy + _engine.GenBigFoodEnergy + _engine.GenCorpseEnergy;
        float foodPct = totalEnergySrc > 0 ? _engine.GenFoodEnergy / totalEnergySrc * 100f : 0;
        float bigFoodPct = totalEnergySrc > 0 ? _engine.GenBigFoodEnergy / totalEnergySrc * 100f : 0;
        float corpsePct = totalEnergySrc > 0 ? _engine.GenCorpseEnergy / totalEnergySrc * 100f : 0;

        var stats = new
        {
            attack_rate = _engine.GenTotalTicks > 0 ? (float)_engine.GenAttacks / _engine.GenTotalTicks : 0f,
            hide_usage_rate = _engine.GenTotalTicks > 0 ? (float)_engine.GenHidingTicks / (_engine.AgentCount * Math.Max(1, _engine.GenTotalTicks)) : 0f,
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
            agent_count = n,
            total_energy = _engine.TotalEnergyInWorld,
            tick_exceptions = _engine.TickExceptionCount,
            agents,
            food,
            corpses,
            river,
            grid_width = ZHI.Shared.ToolDefinitions.GridWidth,
            grid_height = ZHI.Shared.ToolDefinitions.GridHeight,
            stats
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
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

    private async Task HandleWebSocket(HttpListenerContext context, CancellationToken ct)
    {
        WebSocket ws;
        try
        {
            var wsContext = await context.AcceptWebSocketAsync(null);
            ws = wsContext.WebSocket;
        }
        catch
        {
            context.Response.StatusCode = 400;
            context.Response.Close();
            return;
        }

        lock (_clientsLock)
        {
            _clients.Add(ws);
        }

        await SendInitialData(ws);

        var buffer = new byte[1024];
        try
        {
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await ws.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;
            }
        }
        catch { }
        finally
        {
            lock (_clientsLock)
            {
                _clients.Remove(ws);
            }
            try { ws.Dispose(); } catch { }
        }
    }

    private async Task SendInitialData(WebSocket ws)
    {
        // Send log history
        List<string> logs;
        lock (_logLock)
        {
            logs = new List<string>(_logBuffer);
        }
        foreach (var msg in logs)
        {
            var payload = JsonSerializer.Serialize(new
            {
                type = "log",
                time = "",
                message = msg
            });
            await SendAsync(ws, payload);
        }
    }

    private async Task BroadcastLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(200, ct); }
            catch (OperationCanceledException) { break; }

            var payload = JsonSerializer.Serialize(new
            {
                type = "cosmos",
                data = JsonSerializer.Deserialize<object>(BuildCosmosPayload())
            });

            await BroadcastAsync(payload);

            // Broadcast events if any
            var events = _engine.TickEvents;
            if (events.Count > 0)
            {
                var eventPayload = JsonSerializer.Serialize(new
                {
                    type = "events",
                    data = events
                }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
                await BroadcastAsync(eventPayload);
            }
        }
    }

    private async Task BroadcastAsync(string payload)
    {
        List<WebSocket> clients;
        lock (_clientsLock)
        {
            clients = new List<WebSocket>(_clients);
        }

        var dead = new List<WebSocket>();
        foreach (var ws in clients)
        {
            if (ws.State != WebSocketState.Open)
            {
                dead.Add(ws);
                continue;
            }
            try { await SendAsync(ws, payload); }
            catch { dead.Add(ws); }
        }

        if (dead.Count > 0)
        {
            lock (_clientsLock)
            {
                foreach (var ws in dead)
                    _clients.Remove(ws);
            }
        }
    }

    private static async Task SendAsync(WebSocket ws, string payload)
    {
        var bytes = Encoding.UTF8.GetBytes(payload);
        await ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public void Dispose()
    {
        try { _listener.Stop(); } catch { }
        lock (_clientsLock)
        {
            foreach (var ws in _clients)
                try { ws.Dispose(); } catch { }
            _clients.Clear();
        }
    }
}
