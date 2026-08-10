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

    /// <summary>
    /// Whether this file can be previewed (image or text file based on extension).
    /// Determined when the item is populated from OSS.
    /// </summary>
    public bool CanPreview { get; set; }

    /// <summary>
    /// Whether this file is a text file (can be edited / content copied).
    /// Subset of <see cref="CanPreview"/> — excludes binary/image files.
    /// </summary>
    public bool IsTextFile { get; set; }
}
