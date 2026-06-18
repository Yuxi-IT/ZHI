using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace ZHI.Watcher;

public partial class WebServer
{
    private async Task HandleWebSocket(HttpListenerContext context, CancellationToken ct, bool logChannel)
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

        var clients = logChannel ? _logClients : _stateClients;
        var clientsLock = logChannel ? _logClientsLock : _stateClientsLock;

        lock (clientsLock) { clients.Add(ws); }

        if (!logChannel)
            await SendInitialData(ws);
        else
            await SendLogHistory(ws);

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
            lock (clientsLock) { clients.Remove(ws); }
            try { ws.Dispose(); } catch { }
        }
    }

    private async Task SendInitialData(WebSocket ws)
    {
    }

    private async Task SendLogHistory(WebSocket ws)
    {
        List<string> logs;
        lock (_logLock) { logs = new List<string>(_logBuffer); }
        foreach (var msg in logs)
        {
            var payload = JsonSerializer.Serialize(new { type = "log", time = "", message = msg });
            await SendAsync(ws, payload);
        }
    }

    private async Task BroadcastLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(200, ct); }
            catch (OperationCanceledException) { break; }

            try
            {
                var statePayload = JsonSerializer.Serialize(new
                {
                    type = "cosmos",
                    data = JsonSerializer.Deserialize<object>(BuildCosmosPayload())
                });
                await BroadcastToStateClientsAsync(statePayload);

                var events = _engine.TickEvents;
                if (events.Count > 0)
                {
                    var eventPayload = JsonSerializer.Serialize(new
                    {
                        type = "events",
                        data = events
                    }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
                    await BroadcastToLogClientsAsync(eventPayload);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebServer] Broadcast error: {ex.Message}");
            }
        }
    }

    private async Task BroadcastToStateClientsAsync(string payload)
    {
        await BroadcastToAsync(payload, _stateClients, _stateClientsLock);
    }

    private async Task BroadcastToLogClientsAsync(string payload)
    {
        await BroadcastToAsync(payload, _logClients, _logClientsLock);
    }

    private async Task BroadcastToAsync(string payload, List<WebSocket> clientList, Lock clientLock)
    {
        List<WebSocket> clients;
        lock (clientLock) { clients = new List<WebSocket>(clientList); }

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
            lock (clientLock)
            {
                foreach (var ws in dead)
                    clientList.Remove(ws);
            }
        }
    }

    private static async Task SendAsync(WebSocket ws, string payload)
    {
        var bytes = Encoding.UTF8.GetBytes(payload);
        await ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }
}
