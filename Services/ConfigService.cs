using System.Text.Json;
using OssClientPro.Models;

namespace OssClientPro.Services;

/// <summary>
/// Handles loading and saving <see cref="OssConfig"/> to a local config.json file.
/// Uses source-generated JSON for AOT compatibility.
/// </summary>
public class ConfigService
{
    private readonly string _configFilePath;
    private static readonly OssConfigSerializerContext _jsonContext = new(new JsonSerializerOptions { WriteIndented = true });

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
                return JsonSerializer.Deserialize(json, _jsonContext.OssConfig) ?? new OssConfig();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Failed to load config: {ex.Message}");
                return new OssConfig();
            }
        });
    }

    /// <summary>
    /// Saves configuration to config.json.
    /// </summary>
    public Task SaveConfigAsync(OssConfig config)
    {
        return Task.Run(() =>
        {
            try
            {
                var json = JsonSerializer.Serialize(config, _jsonContext.OssConfig);
                File.WriteAllText(_configFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Failed to save config: {ex.Message}");
            }
        });
    }
}
