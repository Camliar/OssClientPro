using System.Collections.ObjectModel;
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
    /// Display name — the key with the configured base prefix stripped
    /// (e.g. "backend/dev/app.log"). Kept complete on purpose: downloads and
    /// extension checks rely on the relative path, not just the file name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Last path segment of <see cref="Name"/>, used as the tree node label
    /// (e.g. "app.log"). For directory nodes this is the directory name.
    /// </summary>
    public string LeafName { get; set; } = string.Empty;

    /// <summary>
    /// Whether this node mirrors a directory in the key hierarchy rather than
    /// an OSS object. Directory nodes exist only in the tree view and are never
    /// uploaded, downloaded or deleted.
    /// </summary>
    public bool IsFolder { get; set; }

    /// <summary>
    /// Whether this real OSS object is a directory placeholder, i.e. its key ends
    /// with '/'. The console creates one of these when you add a folder. Unlike
    /// <see cref="IsFolder"/> it is still a deleteable object, so it keeps the
    /// normal file menu — it is only drawn with a folder glyph.
    /// </summary>
    public bool IsDirectoryPlaceholder { get; set; }

    /// <summary>
    /// Child nodes of a directory node. Always empty for file nodes.
    /// </summary>
    public ObservableCollection<OssObjectItem> Children { get; } = [];

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
    /// Whether this node is expanded in the tree view. Directory nodes start
    /// expanded so the hierarchy is readable right after a refresh.
    /// </summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; } = true;

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
