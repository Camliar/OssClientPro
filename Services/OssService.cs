using OssClientPro.Models;

namespace OssClientPro.Services;

/// <summary>
/// Wraps <see cref="OssHttpClient"/> providing a clean API for the ViewModels.
/// All operations are NativeAOT-compatible (no reflection, no OSS SDK).
/// </summary>
public class OssService
{
    private readonly OssHttpClient _client = new();
    private OssConfig? _config;

    public bool IsInitialized => _config != null;

    public void Initialize(OssConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
        _client.Initialize(config.Endpoint, config.AccessKeyId, config.AccessKeySecret);
    }

    public async Task<List<string>> ListBucketsAsync()
    {
        EnsureInit();
        return await _client.ListBucketsAsync();
    }

    public async Task<List<OssObjectItem>> ListObjectsAsync(string bucket, string? prefix = null)
    {
        EnsureInit();
        return await _client.ListObjectsAsync(bucket, prefix);
    }

    public async Task UploadFileAsync(string bucket, string key, string localPath,
        IProgress<double>? progress = null)
    {
        EnsureInit();
        await _client.UploadFileAsync(bucket, key, localPath, progress);
    }

    public async Task DownloadFileAsync(string bucket, string key, string localPath,
        IProgress<double>? progress = null)
    {
        EnsureInit();
        await _client.DownloadFileAsync(bucket, key, localPath, progress);
    }

    public async Task DeleteFileAsync(string bucket, string key)
    {
        EnsureInit();
        await _client.DeleteFileAsync(bucket, key);
    }

    public string GeneratePresignedUrl(string bucket, string key, int expireSeconds = 3600)
    {
        EnsureInit();
        return _client.GeneratePresignedUrl(bucket, key, expireSeconds);
    }

    private void EnsureInit()
    {
        if (_config == null)
            throw new InvalidOperationException(
                "OssService not initialized. Call Initialize() first.");
    }
}
