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
                // Encrypt sensitive fields before persisting
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
                    Language = config.Language
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

    // ─── DPAPI encryption (Windows user-account-bound) ───

    private static string Encrypt(string plainText)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        var cipher = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(cipher);
    }

    private static string TryDecrypt(string cipherText)
    {
        try
        {
            var cipher = Convert.FromBase64String(cipherText);
            var bytes = ProtectedData.Unprotect(cipher, null, DataProtectionScope.CurrentUser);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            // If decryption fails (e.g., old plain-text config), return as-is
            return cipherText;
        }
    }
}
