using System;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Forza6Client.Services;

public class BridgeService : IDisposable
{
    public const string DefaultHost = "161.33.90.28";
    public const int DefaultPort = 20441;

    private string _remoteHost = DefaultHost;
    private int _remotePort = DefaultPort;
    private UdpClient? _listenSocket;
    private UdpClient? _remoteSocket;
    private CancellationTokenSource? _cts;
    private string _lastError = "";

    public event EventHandler<string>? StatusChanged;
    public event EventHandler<string>? Error;

    public string Status { get; private set; } = "idle";

    public async Task Start(SettingsService cfg, string? remoteHost = null, int? remotePort = null)
    {
        _remoteHost = remoteHost ?? _remoteHost;
        _remotePort = remotePort ?? _remotePort;

        await Stop();
        _cts = new CancellationTokenSource();

        if (string.IsNullOrWhiteSpace(cfg.Username))
        {
            SetStatus("idle");
            return;
        }

        SetStatus("starting");

        try
        {
            _remoteSocket = new UdpClient();
            _remoteSocket.Connect(_remoteHost, _remotePort);

            SetStatus("connected");
            SendHello(cfg);
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            SetStatus("error");
            Error?.Invoke(this, _lastError);
            return;
        }

        try
        {
            _listenSocket = new UdpClient(new System.Net.IPEndPoint(
                System.Net.IPAddress.Parse(cfg.ListenHost), cfg.ListenPort));

            SetStatus("listening");
            _ = ListenLoop(_cts.Token);
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            SetStatus("error");
            Error?.Invoke(this, _lastError);
        }
    }

    public async Task Restart(SettingsService cfg)
    {
        await Stop();
        await Start(cfg);
    }

    public Task Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        _listenSocket?.Close();
        _listenSocket?.Dispose();
        _listenSocket = null;

        _remoteSocket?.Close();
        _remoteSocket?.Dispose();
        _remoteSocket = null;

        _lastError = "";
        SetStatus("idle");
        return Task.CompletedTask;
    }

    private async Task ListenLoop(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var result = await _listenSocket!.ReceiveAsync(ct);
                if (_remoteSocket != null && !ct.IsCancellationRequested)
                {
                    if (result.Buffer.Length >= 324 && IsZeroPacket(result.Buffer))
                        continue;

                    await _remoteSocket.SendAsync(result.Buffer, ct);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
        catch (ObjectDisposedException)
        {
            // socket closed
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            SetStatus("error");
            Error?.Invoke(this, _lastError);
        }
    }

    private void SendHello(SettingsService cfg)
    {
        if (_remoteSocket == null) return;

        var hello = new HelloPacket("hello", cfg.Username, cfg.MarkerColor, "forza-6-client");
        var json = JsonSerializer.Serialize(hello, JsonContext.Default.HelloPacket);
        var buf = Encoding.UTF8.GetBytes(json);
        try
        {
            _remoteSocket.Send(buf, buf.Length);
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            SetStatus("error");
            Error?.Invoke(this, _lastError);
        }
    }

    private static bool IsZeroPacket(byte[] buf)
    {
        var speed = BitConverter.ToSingle(buf, 256);
        var rpm = BitConverter.ToSingle(buf, 16);
        return speed == 0f && rpm == 0f;
    }

    private void SetStatus(string status)
    {
        Status = status;
        StatusChanged?.Invoke(this, status);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _listenSocket?.Close();
        _listenSocket?.Dispose();
        _remoteSocket?.Close();
        _remoteSocket?.Dispose();
    }
}
