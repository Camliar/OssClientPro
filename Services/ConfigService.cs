using System.Security.Cryptography;
using System.Text.Json;
using OssClientPro.Models;

namespace OssClientPro.Services;

/// <summary>
/// Handles loading and saving <see cref="OssConfig"/> to a local config.json file.
/// Sensitive fields (AccessKeyId, AccessKeySecret) are encrypted with DPAPI.
/// </summary>
public class ConfigService
{
    private readonly string _configFilePath;
    private static readonly OssConfigSerializerContext _jsonContext = new(new JsonSerializerOptions { WriteIndented = true });

    public ConfigService()
    {
        _configFilePath = Path.Combine(AppContext.BaseDirectory, "config.json");
    }

    public Task<OssConfig> LoadConfigAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                if (!File.Exists(_configFilePath))
                    return new OssConfig();

                var json = File.ReadAllText(_configFilePath);
                var config = JsonSerializer.Deserialize(json, _jsonContext.OssConfig) ?? new OssConfig();

                // Decrypt sensitive fields
                if (!string.IsNullOrEmpty(config.AccessKeyId))
                    config.AccessKeyId = TryDecrypt(config.AccessKeyId);
                if (!string.IsNullOrEmpty(config.AccessKeySecret))
                    config.AccessKeySecret = TryDecrypt(config.AccessKeySecret);

                return config;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Failed to load config: {ex.Message}");
                App.Log?.Error("Config load failed", ex);
                return new OssConfig();
            }
        });
    }

    public Task SaveConfigAsync(OssConfig config)
    {
        return Task.Run(() =>
        {
            try
            {
                // Encrypt sensitive fields before persisting.
                // NOTE: this is an explicit allow-list — any new OssConfig property
                // must be copied here too, or it will be silently dropped on save.
                var toSave = new OssConfig
                {
                    Endpoint = config.Endpoint,
                    AccessKeyId = string.IsNullOrEmpty(config.AccessKeyId)
                        ? "" : Encrypt(config.AccessKeyId),
                    AccessKeySecret = string.IsNullOrEmpty(config.AccessKeySecret)
                        ? "" : Encrypt(config.AccessKeySecret),
                    Region = config.Region,
                    DefaultBucket = config.DefaultBucket,
                    BasePrefix = config.BasePrefix,
                    Language = config.Language,
                    TimeFormat = config.TimeFormat,
                    ViewMode = config.ViewMode,
                    CleanableDays = config.CleanableDays,
                    CleanablePaths = config.CleanablePaths
                };

                var json = JsonSerializer.Serialize(toSave, _jsonContext.OssConfig);
                File.WriteAllText(_configFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Failed to save config: {ex.Message}");
                App.Log?.Error("Config save failed", ex);
            }
        });
    }

    // ─── Encryption (Windows: DPAPI, macOS/Linux: base64 obfuscation) ───

    private static string Encrypt(string plainText)
    {
#if WINDOWS
        var bytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        var cipher = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(cipher);
#else
        // Non-Windows: base64 obfuscation (not true encryption, but hides from casual view)
        return "B64:" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plainText));
#endif
    }

    private static string TryDecrypt(string cipherText)
    {
        try
        {
#if WINDOWS
            var cipher = Convert.FromBase64String(cipherText);
            var bytes = ProtectedData.Unprotect(cipher, null, DataProtectionScope.CurrentUser);
            return System.Text.Encoding.UTF8.GetString(bytes);
#else
            if (cipherText.StartsWith("B64:"))
                return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cipherText[4..]));
            return cipherText;
#endif
        }
        catch
        {
            return cipherText;
        }
    }
}
