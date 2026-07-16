using CommunityToolkit.Mvvm.ComponentModel;

namespace OssClientPro.Models;

/// <summary>
/// Represents a single OSS object (file) in a bucket.
/// </summary>
public partial class OssObjectItem : ObservableObject
{
    /// <summary>
    /// Object key (full path within the bucket).
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Display name (file name extracted from the key).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Human-readable file size (e.g. "1.5 MB").
    /// </summary>
    public string SizeDisplay { get; set; } = string.Empty;

    /// <summary>
    /// Last modified timestamp (UTC).
    /// </summary>
    public DateTime LastModified { get; set; }

    /// <summary>
    /// Localized last-modified display string.
    /// </summary>
    public string LastModifiedDisplay { get; set; } = string.Empty;

    /// <summary>
    /// Whether this item is checked for batch operations (delete, download, etc.).
    /// </summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
