using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using OssClientPro.Models;

namespace OssClientPro.Services;

/// <summary>
/// NativeAOT-compatible OSS HTTP client. Bypasses the Aliyun SDK (which uses
/// XmlSerializer/reflection) by calling the OSS REST API directly with manual
/// request signing and <see cref="XDocument"/>-based XML parsing.
/// </summary>
public class OssHttpClient
{
    private HttpClient _http = new();
    private string _accessKeyId = string.Empty;
    private string _accessKeySecret = string.Empty;
    private string _endpoint = string.Empty;
    private bool _initialized;

    /// <summary>Budget for small control-plane calls — list, head, delete.</summary>
    private static readonly TimeSpan ControlTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Worst-case throughput assumed when sizing a transfer budget. A 64 KB/s floor
    /// keeps slow links working while still capping a genuinely stalled transfer.
    /// </summary>
    private const long MinTransferBytesPerSecond = 64 * 1024;

    /// <summary>
    /// Timeout for a request that moves <paramref name="payloadBytes"/>: the control
    /// allowance plus a budget sized by the payload.
    /// </summary>
    /// <remarks>
    /// <see cref="HttpClient.Timeout"/> spans the whole operation including the body,
    /// so a single fixed value cannot serve both a 1 KB list call and a 500 MB upload.
    /// A hard 30s here used to abort large uploads mid-body; OSS discards the truncated
    /// PUT, leaving the remote object unchanged.
    /// </remarks>
    private static TimeSpan TransferTimeout(long payloadBytes)
        => ControlTimeout + TimeSpan.FromSeconds((double)payloadBytes / MinTransferBytesPerSecond);

    public void Initialize(string endpoint, string accessKeyId, string accessKeySecret)
    {
        _endpoint = endpoint.Trim();
        _accessKeyId = accessKeyId.Trim();
        _accessKeySecret = accessKeySecret.Trim();

        // No global timeout — every request carries its own budget, because uploads
        // and downloads stream bodies that a fixed value would cut off mid-transfer.
        _http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        _initialized = true;
    }

    // ════════════════════ List buckets ════════════════════

    /// <summary>
    /// Returns all bucket names for the account. Uses the base OSS endpoint.
    /// </summary>
    public async Task<List<string>> ListBucketsAsync()
    {
        EnsureInit();
        var resp = await SendAsync(HttpMethod.Get, null, "/");
        var doc = XDocument.Parse(resp);
        var ns = doc.Root!.Name.Namespace;

        return doc.Root.Elements(ns + "Buckets")
            .Elements(ns + "Bucket")
            .Select(b => b.Element(ns + "Name")?.Value ?? "")
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();
    }

    // ════════════════════ List objects ════════════════════

    public async Task<List<OssObjectItem>> ListObjectsAsync(string bucket, string? prefix)
    {
        EnsureInit();
        var query = "?list-type=2&max-keys=1000";
        if (!string.IsNullOrEmpty(prefix)) query += $"&prefix={Uri.EscapeDataString(prefix)}";

        var resp = await SendAsync(HttpMethod.Get, bucket, "/", query);
        var doc = XDocument.Parse(resp);

        var ns = doc.Root!.Name.Namespace;
        var items = new List<OssObjectItem>();

        foreach (var c in doc.Root.Elements(ns + "Contents"))
        {
            items.Add(new OssObjectItem
            {
                Key = c.Element(ns + "Key")?.Value ?? "",
                Size = long.TryParse(c.Element(ns + "Size")?.Value, out var s) ? s : 0,
                LastModified = DateTime.TryParse(c.Element(ns + "LastModified")?.Value, out var d) ? d : DateTime.MinValue
            });
        }
        return items;
    }

    // ════════════════════ Upload ════════════════════

    public async Task UploadFileAsync(string bucket, string key, string localPath,
        IProgress<double>? progress = null)
    {
        EnsureInit();
        var fileInfo = new FileInfo(localPath);
        await using var stream = File.OpenRead(localPath);

        var content = new ProgressableStreamContent(stream, fileInfo.Length, progress);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        await SendAsync(HttpMethod.Put, bucket, "/" + key, null, content,
            TransferTimeout(fileInfo.Length));
    }

    // ════════════════════ Head ════════════════════

    /// <summary>
    /// Fetches the metadata of a single object.
    /// </summary>
    /// <returns>
    /// The object metadata, or <c>null</c> when the object does not exist.
    /// A missing object is a normal answer here — callers probe before uploading.
    /// </returns>
    public async Task<OssObjectHead?> HeadObjectAsync(string bucket, string key)
    {
        EnsureInit();
        using var resp = await SendRawAsync(HttpMethod.Head, bucket, "/" + key);

        if (resp.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"OSS {(int)resp.StatusCode}: HEAD {key} failed");

        return new OssObjectHead
        {
            ETag = resp.Headers.ETag?.Tag.Trim('"') ?? string.Empty,
            Length = resp.Content.Headers.ContentLength ?? 0,
            LastModified = resp.Content.Headers.LastModified ?? DateTimeOffset.MinValue
        };
    }

    // ════════════════════ Download ════════════════════

    public async Task DownloadFileAsync(string bucket, string key, string localPath,
        IProgress<double>? progress = null)
    {
        EnsureInit();

        // Headers first, so the body budget below can be sized from Content-Length.
        using var resp = await SendRawAsync(HttpMethod.Get, bucket, "/" + key, null, null,
            ControlTimeout, HttpCompletionOption.ResponseHeadersRead);

        if (!resp.IsSuccessStatusCode)
        {
            var error = await resp.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"OSS {(int)resp.StatusCode}: {Truncate(error, 200)}");
        }

        var total = resp.Content.Headers.ContentLength ?? 0;
        using var cts = new CancellationTokenSource(TransferTimeout(total));

        await using var src = await resp.Content.ReadAsStreamAsync(cts.Token);
        await using var dst = File.Create(localPath);

        var buf = new byte[81920];
        long read = 0;
        int n;
        while ((n = await src.ReadAsync(buf, cts.Token)) > 0)
        {
            await dst.WriteAsync(buf.AsMemory(0, n), cts.Token);
            read += n;
            if (total > 0) progress?.Report((double)read / total * 100.0);
        }
        progress?.Report(100.0);
    }

    // ════════════════════ Delete ════════════════════

    public async Task DeleteFileAsync(string bucket, string key)
    {
        EnsureInit();
        await SendAsync(HttpMethod.Delete, bucket, "/" + key);
    }

    // ════════════════════ Pre-signed URL ════════════════════

    public string GeneratePresignedUrl(string bucket, string key, int expireSeconds = 3600)
    {
        EnsureInit();
        var expires = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + expireSeconds;
        var resource = $"/{bucket}/{key.TrimStart('/')}";

        var stringToSign = $"GET\n\n\n{expires}\n{resource}";
        var signature = HmacSha1(_accessKeySecret, stringToSign);
        var encodedSig = Uri.EscapeDataString(Convert.ToBase64String(signature));

        return $"https://{bucket}.{_endpoint}/{key.TrimStart('/')}" +
               $"?OSSAccessKeyId={Uri.EscapeDataString(_accessKeyId)}" +
               $"&Expires={expires}&Signature={encodedSig}";
    }

    // ════════════════════ Core HTTP ════════════════════

    private async Task<string> SendAsync(HttpMethod method, string? bucket,
        string path, string? query = null, HttpContent? content = null,
        TimeSpan? timeout = null)
    {
        using var resp = await SendRawAsync(method, bucket, path, query, content, timeout);
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"OSS {(int)resp.StatusCode}: {Truncate(body, 200)}");
        }
        return body;
    }

    private async Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string? bucket,
        string path, string? query = null, HttpContent? content = null,
        TimeSpan? timeout = null,
        HttpCompletionOption completion = HttpCompletionOption.ResponseContentRead)
    {
        // ListBuckets uses the base endpoint; ListObjects/Upload/etc use bucket-specific
        var host = bucket != null ? $"{bucket}.{_endpoint}" : _endpoint;
        var uri = $"https://{host}{path}";
        if (!string.IsNullOrEmpty(query)) uri += query;

        var req = new HttpRequestMessage(method, uri) { Content = content };

        // Add Date header for signing
        var date = DateTime.UtcNow.ToString("R");
        req.Headers.Add("Date", date);
        req.Headers.Host = host;

        // Sign the request
        var signature = Sign(method.Method, date, bucket, path, content?.Headers.ContentType?.ToString());
        req.Headers.Authorization = new AuthenticationHeaderValue("OSS",
            $"{_accessKeyId}:{signature}");

        using var cts = new CancellationTokenSource(timeout ?? ControlTimeout);
        return await _http.SendAsync(req, completion, cts.Token);
    }

    /// <summary>
    /// OSS V1 signature: base64(HMAC-SHA1(secret, stringToSign))
    /// </summary>
    private string Sign(string method, string date, string? bucket, string path,
        string? contentType)
    {
        contentType ??= "";
        // ListBuckets has no bucket in the resource path
        var resource = bucket != null ? $"/{bucket}{path}" : path;
        var stringToSign = $"{method}\n\n{contentType}\n{date}\n{resource}";
        var sig = HmacSha1(_accessKeySecret, stringToSign);
        return Convert.ToBase64String(sig);
    }

    private static byte[] HmacSha1(string key, string data)
    {
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(key));
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }

    private void EnsureInit()
    {
        if (!_initialized)
            throw new InvalidOperationException("OssHttpClient not initialized");
    }

    private static string Truncate(string s, int len)
        => s.Length <= len ? s : s[..len] + "...";
}

/// <summary>
/// Wraps a stream for upload progress reporting.
/// NativeAOT-safe — no reflection, no serialization.
/// </summary>
internal sealed class ProgressableStreamContent : HttpContent
{
    private readonly Stream _stream;
    private readonly long _total;
    private readonly IProgress<double>? _progress;
    private const int BufferSize = 81920;

    public ProgressableStreamContent(Stream stream, long total, IProgress<double>? progress)
    {
        _stream = stream;
        _total = total;
        _progress = progress;
    }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        var buf = new byte[BufferSize];
        long sent = 0;
        int n;
        while ((n = await _stream.ReadAsync(buf)) > 0)
        {
            await stream.WriteAsync(buf.AsMemory(0, n));
            sent += n;
            _progress?.Report((double)sent / _total * 100.0);
        }
        _progress?.Report(100.0);
    }

    protected override bool TryComputeLength(out long length)
    {
        length = _total;
        return true;
    }
}
