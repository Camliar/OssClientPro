namespace OssClientPro.Models;

/// <summary>
/// Metadata of a single OSS object, as returned by a HEAD request.
/// Used to probe whether an object exists before an upload and to confirm
/// afterwards that the write actually replaced it.
/// </summary>
public sealed class OssObjectHead
{
    /// <summary>
    /// Entity tag of the object. For a simple (non-multipart) PUT this is the
    /// MD5 of the content.
    /// </summary>
    public string ETag { get; set; } = string.Empty;

    /// <summary>
    /// Object size in bytes.
    /// </summary>
    public long Length { get; set; }

    /// <summary>
    /// Server-side last modified timestamp.
    /// </summary>
    public DateTimeOffset LastModified { get; set; }
}
