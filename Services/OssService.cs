using Aliyun.OSS;
using OssClientPro.Models;

namespace OssClientPro.Services;

/// <summary>
/// Encapsulates all Aliyun OSS operations.
/// Must be initialized with <see cref="Initialize"/> before use.
/// </summary>
public class OssService
{
    private OssClient? _client;
    private OssConfig? _config;

    /// <summary>
    /// Whether the service has been initialized with valid credentials.
    /// </summary>
    public bool IsInitialized => _client != null;

    /// <summary>
    /// Initializes (or re-initializes) the underlying <see cref="OssClient"/> with the given config.
    /// </summary>
    public void Initialize(OssConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        _config = config;
        _client = new OssClient(config.Endpoint, config.AccessKeyId, config.AccessKeySecret);
    }

    /// <summary>
    /// Lists all buckets owned by the authenticated account.
    /// </summary>
    public async Task<List<Bucket>> ListBucketsAsync()
    {
        EnsureInitialized();

        try
        {
            var buckets = await Task.Run(() => _client!.ListBuckets());
            return [.. buckets];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OssService] ListBucketsAsync failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Lists objects in the specified bucket, optionally filtered by prefix.
    /// </summary>
    public async Task<List<OssObjectItem>> ListObjectsAsync(string bucketName, string? prefix = null)
    {
        EnsureInitialized();

        try
        {
            var request = new ListObjectsRequest(bucketName)
            {
                Prefix = prefix
            };

            var result = await Task.Run(() => _client!.ListObjects(request));

            return [.. result.ObjectSummaries
                .Select(o => new OssObjectItem
                {
                    Key = o.Key,
                    Name = Path.GetFileName(o.Key),
                    Size = o.Size,
                    LastModified = o.LastModified
                })];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OssService] ListObjectsAsync failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Uploads a local file to OSS with progress reporting.
    /// </summary>
    /// <param name="bucketName">Target bucket name.</param>
    /// <param name="objectName">Object key (path) in the bucket.</param>
    /// <param name="localFilePath">Local file path to upload.</param>
    /// <param name="progress">Progress reporter; receives values 0.0–100.0.</param>
    public async Task UploadFileAsync(
        string bucketName,
        string objectName,
        string localFilePath,
        IProgress<double>? progress = null)
    {
        EnsureInitialized();

        try
        {
            // Use stream-based upload for progress tracking
            var fileInfo = new FileInfo(localFilePath);
            var totalBytes = fileInfo.Length;
            long uploadedBytes = 0;

            await using var fileStream = File.OpenRead(localFilePath);

            // Use a callback to report progress
            using var progressStream = new ProgressStream(fileStream, bytesRead =>
            {
                uploadedBytes += bytesRead;
                var percentage = (double)uploadedBytes / totalBytes * 100.0;
                progress?.Report(Math.Min(percentage, 100.0));
            });

            await Task.Run(() => _client!.PutObject(bucketName, objectName, progressStream));
            progress?.Report(100.0);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OssService] UploadFileAsync failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Downloads a file from OSS to a local path with progress reporting.
    /// </summary>
    public async Task DownloadFileAsync(
        string bucketName,
        string objectName,
        string localFilePath,
        IProgress<double>? progress = null)
    {
        EnsureInitialized();

        try
        {
            // Get object metadata first to know the total size
            var metadata = await Task.Run(() => _client!.GetObjectMetadata(bucketName, objectName));
            var totalBytes = metadata.ContentLength;
            long downloadedBytes = 0;

            var obj = await Task.Run(() => _client!.GetObject(bucketName, objectName));

            await using var sourceStream = obj.Content;
            await using var fileStream = File.Create(localFilePath);

            var buffer = new byte[81920]; // 80KB buffer
            int bytesRead;
            while ((bytesRead = await sourceStream.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                downloadedBytes += bytesRead;
                var percentage = totalBytes > 0
                    ? (double)downloadedBytes / totalBytes * 100.0
                    : 0.0;
                progress?.Report(Math.Min(percentage, 100.0));
            }

            progress?.Report(100.0);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OssService] DownloadFileAsync failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Deletes a single object from the specified bucket.
    /// </summary>
    public async Task DeleteFileAsync(string bucketName, string objectName)
    {
        EnsureInitialized();

        try
        {
            await Task.Run(() => _client!.DeleteObject(bucketName, objectName));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OssService] DeleteFileAsync failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Generates a pre-signed URL for temporary access to a private object.
    /// </summary>
    /// <param name="bucketName">Bucket name.</param>
    /// <param name="objectName">Object key.</param>
    /// <param name="expireSeconds">URL validity duration in seconds (default: 3600 = 1 hour).</param>
    public string GeneratePresignedUrl(string bucketName, string objectName, int expireSeconds = 3600)
    {
        EnsureInitialized();

        try
        {
            var url = _client!.GeneratePresignedUri(bucketName, objectName,
                DateTime.UtcNow.AddSeconds(expireSeconds),
                SignHttpMethod.Get);
            return url.AbsoluteUri;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OssService] GeneratePresignedUrl failed: {ex.Message}");
            throw;
        }
    }

    private void EnsureInitialized()
    {
        if (_client == null)
            throw new InvalidOperationException(
                "OssService is not initialized. Call Initialize() with valid credentials first.");
    }
}

/// <summary>
/// A wrapper stream that reports the number of bytes read via a callback.
/// Used for upload progress tracking.
/// </summary>
internal sealed class ProgressStream : Stream
{
    private readonly Stream _innerStream;
    private readonly Action<long> _onBytesRead;

    public ProgressStream(Stream innerStream, Action<long> onBytesRead)
    {
        _innerStream = innerStream ?? throw new ArgumentNullException(nameof(innerStream));
        _onBytesRead = onBytesRead ?? throw new ArgumentNullException(nameof(onBytesRead));
    }

    public override bool CanRead => _innerStream.CanRead;
    public override bool CanSeek => _innerStream.CanSeek;
    public override bool CanWrite => _innerStream.CanWrite;
    public override long Length => _innerStream.Length;

    public override long Position
    {
        get => _innerStream.Position;
        set => _innerStream.Position = value;
    }

    public override void Flush() => _innerStream.Flush();

    public override int Read(byte[] buffer, int offset, int count)
    {
        var bytesRead = _innerStream.Read(buffer, offset, count);
        if (bytesRead > 0) _onBytesRead(bytesRead);
        return bytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin) => _innerStream.Seek(offset, origin);

    public override void SetLength(long value) => _innerStream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) => _innerStream.Write(buffer, offset, count);

    protected override void Dispose(bool disposing)
    {
        if (disposing) _innerStream.Dispose();
        base.Dispose(disposing);
    }
}
