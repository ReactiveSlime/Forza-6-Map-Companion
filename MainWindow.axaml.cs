using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Forza6Client.Services;

namespace Forza6Client;

public partial class MainWindow : Window
{
    private readonly BridgeService _bridge;
    private readonly SettingsService _cfg;
    private readonly ObservableCollection<string> _addrs = [];
    private double _hue;
    private double _sat = 1;
    private double _val = 1;
    private bool _svDrag;
    private bool _hueDrag;
    private bool _hexUpdating;
    private const int SvRes = 200;

    public MainWindow()
    {
        InitializeComponent();
        _cfg = new SettingsService();
        _bridge = new BridgeService();
        AddressList.ItemsSource = _addrs;
        _bridge.StatusChanged += OnStatus;
        _bridge.Error += OnError;
        Loaded += OnLoaded;
        Closing += OnClosing;
        ColorHexInput.TextChanged += OnHexChanged;
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        await _cfg.Load();
        UsernameInput.Text = _cfg.Username;
        HostInput.Text = _cfg.ListenHost;
        PortInput.Text = _cfg.ListenPort.ToString();
        SetSwatch(_cfg.MarkerColor);
        RefreshAddrs();
        await _bridge.Start(_cfg, Program.RemoteHost, Program.RemotePort);
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e) => _bridge.Dispose();

    private void OnStatus(object? sender, string s)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var (bg, fg) = s switch
            {
                "listening" or "connected" => (Color.Parse("#22c55e1f"), Color.Parse("#86efac")),
                "error" => (Color.Parse("#f43f5e1f"), Color.Parse("#fda4af")),
                _ => (Color.Parse("#94a3b81f"), Color.Parse("#cbd5e1")),
            };
            StatusBadge.Background = new SolidColorBrush(bg);
            StatusText.Text = s;
            StatusText.Foreground = new SolidColorBrush(fg);
        });
    }

    private void OnError(object? sender, string msg)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            SavedPathText.Text = $"Error: {msg}";
            SavedPathText.Foreground = new SolidColorBrush(Color.Parse("#fb7185"));
        });
    }

    private void CloseColorPopup(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => ColorPopup.Close();

    private void OpenColorPicker(object? sender, PointerPressedEventArgs e)
    {
        if (Color.TryParse(_cfg.MarkerColor, out var c))
        {
            var (h, s, v) = RgbToHsv(c);
            _hue = h;
            _sat = s;
            _val = v;
        }
        ColorHexInput.Text = _cfg.MarkerColor;
        RenderSvSquare();
        ColorPopup.Open();
    }

    private void OnSvPressed(object? sender, PointerPressedEventArgs e)
    {
        _svDrag = true;
        UpdateSv(e.GetPosition(SvImage));
        e.Pointer.Capture(SvContainer);
    }

    private void OnSvMoved(object? sender, PointerEventArgs e)
    {
        if (!_svDrag) return;
        UpdateSv(e.GetPosition(SvImage));
    }

    private void OnSvReleased(object? sender, PointerReleasedEventArgs e)
    {
        _svDrag = false;
        e.Pointer.Capture(null);
    }

    private void UpdateSv(Point pos)
    {
        var w = SvImage.Bounds.Width;
        var h = SvImage.Bounds.Height;
        if (w <= 0 || h <= 0) return;
        _sat = Math.Clamp(pos.X / w, 0, 1);
        _val = Math.Clamp(1 - pos.Y / h, 0, 1);
        UpdateColor();
    }

    private void OnHuePressed(object? sender, PointerPressedEventArgs e)
    {
        _hueDrag = true;
        UpdateHue(e.GetPosition(HueContainer));
        e.Pointer.Capture(HueContainer);
    }

    private void OnHueMoved(object? sender, PointerEventArgs e)
    {
        if (!_hueDrag) return;
        UpdateHue(e.GetPosition(HueContainer));
    }

    private void OnHueReleased(object? sender, PointerReleasedEventArgs e)
    {
        _hueDrag = false;
        e.Pointer.Capture(null);
    }

private void UpdateHue(Point pos)
{
    var w = HueContainer.Bounds.Width;
    if (w <= 0) return;
    _hue = Math.Clamp(pos.X / w, 0, 1) * 360;
    RenderSvSquare();
    UpdateColor();
    UpdateHueIndicator();
}
    private void RenderSvSquare()
    {
        var bmp = new WriteableBitmap(new PixelSize(SvRes, SvRes), new Vector(96, 96));
        using var fb = bmp.Lock();
        var buf = new byte[SvRes * SvRes * 4];
        for (var y = 0; y < SvRes; y++)
        {
            for (var x = 0; x < SvRes; x++)
            {
                var s = (double)x / (SvRes - 1);
                var v = 1.0 - (double)y / (SvRes - 1);
                var c = HsvToRgb(_hue, s, v);
                var i = (y * SvRes + x) * 4;
                buf[i] = c.B;
                buf[i + 1] = c.G;
                buf[i + 2] = c.R;
                buf[i + 3] = c.A;
            }
        }
        Marshal.Copy(buf, 0, fb.Address, buf.Length);
        SvImage.Source = bmp;
    }

private void UpdateColor()
{
    var c = HsvToRgb(_hue, _sat, _val);
    var h = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    PreviewSwatch.Background = new SolidColorBrush(c);
    _cfg.MarkerColor = h;
    _hexUpdating = true;
    ColorHexInput.Text = h;
    _hexUpdating = false;
    UpdateSvIndicator();
    SetSwatch(h);
}

private void UpdateSvIndicator()
{
    var svW = SvContainer.Bounds.Width;
    var svH = SvContainer.Bounds.Height;
    if (svW > 0 && svH > 0)
    {
        SvIndicator.Margin = new Thickness(_sat * svW - 6, (1 - _val) * svH - 6, 0, 0);
        SvIndicator.IsVisible = true;
    }
}

private void UpdateHueIndicator()
{
    var hueW = HueContainer.Bounds.Width;
    if (hueW > 0)
    {
        HueIndicator.Margin = new Thickness(_hue / 360 * hueW - 2, 0, 0, 0);
        HueIndicator.IsVisible = true;
    }
}



    private static Color HsvToRgb(double h, double s, double v)
    {
        var hi = (int)(h / 60) % 6;
        var f = h / 60 - Math.Floor(h / 60);
        var p = v * (1 - s);
        var q = v * (1 - f * s);
        var t = v * (1 - (1 - f) * s);
        var (r, g, b) = hi switch
        {
            0 => (v, t, p), 1 => (q, v, p), 2 => (p, v, t),
            3 => (p, q, v), 4 => (t, p, v), _ => (v, p, q),
        };
        return Color.FromArgb(255, (byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    private static (double h, double s, double v) RgbToHsv(Color c)
    {
        var r = c.R / 255.0;
        var g = c.G / 255.0;
        var b = c.B / 255.0;
        var mx = Math.Max(r, Math.Max(g, b));
        var mn = Math.Min(r, Math.Min(g, b));
        var df = mx - mn;
        var h = 0.0;
        if (Math.Abs(df) > 1e-10)
            h = mx == r ? (60 * ((g - b) / df) + 360) % 360
              : mx == g ? 60 * ((b - r) / df) + 120
              : 60 * ((r - g) / df) + 240;
        return (h, mx < 1e-10 ? 0 : df / mx, mx);
    }

private void OnHexChanged(object? sender, TextChangedEventArgs e)
{
    if (_hexUpdating) return;
    var text = ColorHexInput.Text?.Trim() ?? "";
    if (text.Length == 7 && text[0] == '#' && Color.TryParse(text, out var c))
    {
        var (hue, sat, val) = RgbToHsv(c);
        _hue = hue;
        _sat = sat;
        _val = val;
        RenderSvSquare();
        PreviewSwatch.Background = new SolidColorBrush(c);
        _cfg.MarkerColor = text;
        UpdateSvIndicator();
        UpdateHueIndicator();
        SetSwatch(text);
    }
}

    private void SetSwatch(string h)
    {
        if (!Color.TryParse(h, out var c)) return;
        ColorSwatch.Background = new SolidColorBrush(c);
        ColorHex.Text = h;
    }

    private void RefreshAddrs()
    {
        _addrs.Clear();
        foreach (var ip in GetAddrs())
            _addrs.Add($"{ip}:{_cfg.ListenPort}");
    }

    private static string[] GetAddrs()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up)
                .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                .Where(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork
                             && !IPAddress.IsLoopback(ip.Address))
                .Select(ip => ip.Address.ToString())
                .Distinct()
                .OrderBy(x => x)
                .ToArray();
        }
        catch
        {
            return ["127.0.0.1"];
        }
    }

    private async void SaveSettings(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _cfg.Username = UsernameInput.Text?.Trim() ?? "";
        _cfg.ListenHost = HostInput.Text?.Trim() ?? "0.0.0.0";
        _cfg.ListenPort = int.TryParse(PortInput.Text, out var p) ? p : 20440;
        await _cfg.Save();
        RefreshAddrs();
        SavedPathText.Text = "Settings saved";
        SavedPathText.Foreground = new SolidColorBrush(Color.Parse("#94a3b8"));
        await _bridge.Restart(_cfg);
    }

    private async void RestartBridge(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => await _bridge.Restart(_cfg);

    private async void StopBridge(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => await _bridge.Stop();
}
