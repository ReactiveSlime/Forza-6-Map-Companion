using Avalonia;
using Forza6Client.Services;

namespace Forza6Client;

class Program
{
    public static string RemoteHost = BridgeService.DefaultHost;
    public static int RemotePort = BridgeService.DefaultPort;

    static void Main(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            switch (args[i])
            {
                case "--host" or "-h": RemoteHost = args[++i]; break;
                case "--port" or "-p": RemotePort = int.Parse(args[++i]); break;
            }
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
