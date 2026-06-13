using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Forza6Client.Services;

public class SettingsService
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Forza6Client");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "settings.json");

    public string Username { get; set; } = "";
    public string ListenHost { get; set; } = "0.0.0.0";
    public int ListenPort { get; set; } = 20440;
    public string MarkerColor { get; set; } = "#FF6B35";

    public async Task Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
                return;
            var json = await File.ReadAllTextAsync(ConfigPath);
            var data = JsonSerializer.Deserialize(json, JsonContext.Default.SettingsData);
            if (data != null)
            {
                Username = data.Username ?? "";
                ListenHost = string.IsNullOrWhiteSpace(data.ListenHost)
                    ? "0.0.0.0"
                    : data.ListenHost;
                ListenPort = data.ListenPort > 0 ? data.ListenPort : 20440;
                MarkerColor = string.IsNullOrWhiteSpace(data.MarkerColor)
                    ? "#FF6B35"
                    : data.MarkerColor;
            }
        }
        catch
        {
            // use defaults
        }
    }

    public async Task Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var data = new SettingsData(Username, ListenHost, ListenPort, MarkerColor);
            var json = JsonSerializer.Serialize(data, JsonContext.Default.SettingsData);
            await File.WriteAllTextAsync(ConfigPath, json);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }
}
