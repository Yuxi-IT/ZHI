using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using ZHI.Shared;

namespace ZHI.Watcher;

/// <summary>
/// TCP 通信服务端 - 守望者侧
/// 监听本地端口，等待栀连接
/// </summary>
public class PipeServer : IDisposable
{
    private readonly int _port;
    private TcpListener? _listener;
    private TcpClient? _client;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private CancellationTokenSource _cts = new();
    private Task? _listenTask;

    public event Action<MpcRequest>? OnRequest;
    public bool IsConnected => _client?.Connected ?? false;

    public PipeServer(int port)
    {
        _port = port;
    }

    public async Task StartAsync()
    {
        _listener = new TcpListener(IPAddress.Loopback, _port);
        _listener.Start();

        _client = await _listener.AcceptTcpClientAsync(_cts.Token);
        _client.NoDelay = true;

        var stream = _client.GetStream();
        _reader = new StreamReader(stream, Encoding.UTF8);
        _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

        _listenTask = Task.Run(ListenLoop);
    }

    private async Task ListenLoop()
    {
        try
        {
            while (!_cts.Token.IsCancellationRequested && _client?.Connected == true)
            {
                var line = await _reader!.ReadLineAsync(_cts.Token);
                if (line == null) break;

                var request = JsonSerializer.Deserialize<MpcRequest>(line);
                if (request != null)
                {
                    OnRequest?.Invoke(request);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
    }

    public async Task SendResponseAsync(MpcResponse response)
    {
        if (_writer == null || !IsConnected) return;

        var json = JsonSerializer.Serialize(response);
        await _writer.WriteLineAsync(json);
    }

    public async Task SendDeathSignalAsync(string cause, int generation)
    {
        if (_writer == null || !IsConnected) return;

        var signal = new DeathSignal
        {
            Id = $"death_{generation}",
            Cause = cause,
            Generation = generation
        };

        var json = JsonSerializer.Serialize(signal);
        await _writer.WriteLineAsync(json);
    }

    public void Disconnect()
    {
        _cts.Cancel();
        try { _client?.Close(); } catch { }
        try { _listener?.Stop(); } catch { }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _reader?.Dispose();
        _writer?.Dispose();
        _client?.Dispose();
        _listener?.Stop();
        _cts.Dispose();
    }
}
