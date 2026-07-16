using System.Text.Json;
using OssClientPro.Models;

namespace OssClientPro.Services;

/// <summary>
/// Handles loading and saving <see cref="OssConfig"/> to a local config.json file.
/// </summary>
public class ConfigService
{
    private readonly string _configFilePath;

    /// <summary>
    /// Initializes the config service and determines where config.json is stored.
    /// On desktop, it is placed next to the executable.
    /// </summary>
    public ConfigService()
    {
        var appDir = AppContext.BaseDirectory;
        _configFilePath = Path.Combine(appDir, "config.json");
    }

    /// <summary>
    /// Loads configuration from config.json.
    /// Returns a new <see cref="OssConfig"/> with defaults if the file does not exist.
    /// </summary>
    public Task<OssConfig> LoadConfigAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                if (!File.Exists(_configFilePath))
                    return new OssConfig();

                var json = File.ReadAllText(_configFilePath);
                return JsonSerializer.Deserialize<OssConfig>(json) ?? new OssConfig();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Failed to load config: {ex.Message}");
                return new OssConfig();
            }
        });
    }

    /// <summary>
    /// Saves configuration to config.json with indented formatting.
    /// </summary>
    public Task SaveConfigAsync(OssConfig config)
    {
        return Task.Run(() =>
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(_configFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Failed to save config: {ex.Message}");
            }
        });
    }
}
