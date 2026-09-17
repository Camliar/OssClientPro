using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OssClientPro.Helpers;
using OssClientPro.Models;
using OssClientPro.Services;

namespace OssClientPro.ViewModels;

public delegate Task<bool> ConfirmHandler(string title, string message);

public partial class FileListViewModel : ViewModelBase
{
    private readonly OssService _ossService;
    private readonly MainViewModel _main;

    public ConfirmHandler? ConfirmHandler { get; set; }
    public Window? MainWindow { get; set; }

    public ObservableCollection<string> Buckets { get; } = [];

    /// <summary>Filtered files shown in the DataGrid (after search).</summary>
    public ObservableCollection<OssObjectItem> Files { get; } = [];

    /// <summary>
    /// Hierarchical view of the same objects as <see cref="Files"/>, shown in the
    /// tree view. File nodes are the very same <see cref="OssObjectItem"/> instances
    /// so checkbox selection is shared across both views; only directory nodes are new.
    /// </summary>
    public ObservableCollection<OssObjectItem> TreeNodes { get; } = [];

    [ObservableProperty]
    public partial string? SelectedBucket { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UploadPrefix))]
    [NotifyPropertyChangedFor(nameof(UploadTargetDisplay))]
    [NotifyPropertyChangedFor(nameof(CanActOnFile))]
    [NotifyPropertyChangedFor(nameof(CanDelete))]
    [NotifyPropertyChangedFor(nameof(CanDownload))]
    public partial OssObjectItem? SelectedFile { get; set; }

    /// <summary>
    /// Item selected in the tree view. Deliberately separate from
    /// <see cref="SelectedFile"/>: a directory node is not an OSS object, and the
    /// grid (still bound while hidden) would coerce such a selection back to null.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UploadPrefix))]
    [NotifyPropertyChangedFor(nameof(UploadTargetDisplay))]
    public partial OssObjectItem? SelectedNode { get; set; }

    /// <summary>
    /// Whether the file area shows the tree view instead of the flat list.
    /// Persisted to config.json so the choice survives a restart.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UploadPrefix))]
    [NotifyPropertyChangedFor(nameof(UploadTargetDisplay))]
    public partial bool IsTreeView { get; set; }

    [ObservableProperty]
    public partial double ProgressValue { get; set; }

    [ObservableProperty]
    public partial bool IsProgressVisible { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanActOnFile))]
    [NotifyPropertyChangedFor(nameof(CanDelete))]
    [NotifyPropertyChangedFor(nameof(CanDownload))]
    public partial bool IsFileOperationBusy { get; set; }

    /// <summary>
    /// State of the "select all" checkbox: <c>true</c> when every row currently shown
    /// is checked, <c>false</c> when none is, and <c>null</c> (indeterminate) when only
    /// some are — the same three-state rule as a directory node.
    /// </summary>
    [ObservableProperty]
    public partial bool? IsAllSelected { get; set; }

    /// <summary>
    /// Number of checked files across the whole listing, including rows hidden by the
    /// search filter or collapsed inside a tree node. Batch commands act on exactly
    /// this set, so the count in the status bar always matches what they will do.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDelete))]
    [NotifyPropertyChangedFor(nameof(CanDownload))]
    public partial int SelectedCount { get; set; }

    /// <summary>
    /// Whether a command that acts on one specific file — preview, signed URL — can
    /// run: a file must be selected and no other operation in flight.
    /// </summary>
    public bool CanActOnFile => !IsFileOperationBusy && SelectedFile != null;

    /// <summary>
    /// Whether delete can run. It accepts either a selected file or any checked rows,
    /// so it stays enabled when only checkboxes are ticked.
    /// </summary>
    public bool CanDelete => !IsFileOperationBusy && (SelectedFile != null || SelectedCount > 0);

    /// <summary>
    /// Whether download can run — same rule as delete, because it also downloads every
    /// checked file when there is more than one.
    /// </summary>
    public bool CanDownload => !IsFileOperationBusy && (SelectedFile != null || SelectedCount > 0);

    // ─── Status bar stats ───

    /// <summary>Total objects loaded from OSS (before search filter).</summary>
    [ObservableProperty]
    public partial int TotalCount { get; set; }

    /// <summary>Timestamp of the last successful refresh.</summary>
    [ObservableProperty]
    public partial string LastRefreshTime { get; set; } = string.Empty;

    // ─── Search ───

    /// <summary>Search filter text. Filters files by name/key as you type.</summary>
    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    /// <summary>
    /// Whether the listing is narrowed to files past the cleanup threshold, so the
    /// stale ones can be reviewed and cleared in one pass.
    /// </summary>
    [ObservableProperty]
    public partial bool ShowCleanableOnly { get; set; }

    // ─── Notification toast ───

    /// <summary>Brief toast message shown after operations (auto-dismissed).</summary>
    [ObservableProperty]
    public partial string NotificationMessage { get; set; } = string.Empty;

    /// <summary>Whether the notification toast is currently visible.</summary>
    [ObservableProperty]
    public partial bool IsNotificationVisible { get; set; }

    private CancellationTokenSource? _notificationCts;

    private string _basePrefix = string.Empty;
    private readonly List<OssObjectItem> _allFiles = [];

    /// <summary>Set while <see cref="ApplyViewMode"/> restores the saved mode, so
    /// loading settings does not immediately write them back to disk.</summary>
    private bool _suppressViewModePersist;

    /// <summary>
    /// Set while this view model writes check states itself. Every write raises
    /// <see cref="OssObjectItem.PropertyChanged"/>, and re-entering the roll-up from
    /// inside it would recompute the same tree repeatedly.
    /// </summary>
    private bool _updatingSelection;

    /// <summary>Age at which an object is flagged cleanable. 0 disables the flag.</summary>
    private int _cleanableDays = OssConfig.DefaultCleanableDays;

    /// <summary>Directories the cleanup flag applies to. Empty means every directory.</summary>
    private string[] _cleanableRoots = [];

    public FileListViewModel() : this(new OssService(), null!) { }

    public FileListViewModel(OssService ossService, MainViewModel main)
    {
        _ossService = ossService;
        _main = main;
    }

    public void SetBasePrefix(string prefix)
    {
        _basePrefix = (prefix ?? string.Empty).Trim();
        if (_basePrefix.Length > 0 && !_basePrefix.EndsWith('/'))
            _basePrefix += '/';

        if (!string.IsNullOrEmpty(SelectedBucket))
            _ = LoadFilesAsync();
    }

    partial void OnSelectedBucketChanged(string? value)
    {
        if (!string.IsNullOrEmpty(value))
            _ = LoadFilesAsync();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplySearchFilter();
    }

    partial void OnShowCleanableOnlyChanged(bool value)
    {
        ApplySearchFilter();
    }

    // ──────────────────────── Load ────────────────────────

    [RelayCommand]
    private async Task LoadFilesAsync()
    {
        if (string.IsNullOrEmpty(SelectedBucket)) return;

        try
        {
            IsFileOperationBusy = true;
            _main.StatusMessage = _main.LanguageService["msg_connecting"];

            var prefix = _basePrefix.Length > 0 ? _basePrefix : null;
            App.Log.Info($"ListObjects: bucket={SelectedBucket}, prefix={prefix ?? "(root)"}");
            var objects = await _ossService.ListObjectsAsync(SelectedBucket, prefix);

            _allFiles.Clear();
            foreach (var obj in objects)
            {
                var displayKey = obj.Key;
                if (_basePrefix.Length > 0 && displayKey.StartsWith(_basePrefix, StringComparison.Ordinal))
                    displayKey = displayKey[_basePrefix.Length..];
                obj.Name = string.IsNullOrEmpty(displayKey) ? obj.Key : displayKey;
                // TrimEnd handles directory placeholder objects, whose keys end with '/'.
                obj.LeafName = Path.GetFileName(obj.Name.TrimEnd('/'));
                obj.IsDirectoryPlaceholder = obj.Key.EndsWith('/');
                obj.SizeDisplay = _main.LanguageService.FormatFileSize(obj.Size);
                obj.LastModifiedDisplay = _main.LanguageService.FormatDateTime(obj.LastModified);
                obj.CleanableTooltip = _main.LanguageService["tooltip_cleanable"];
                obj.IsSelected = false;
                UpdateCleanableFlag(obj);
                (obj.CanPreview, obj.IsTextFile) = ClassifyFile(obj.Name);
                obj.PropertyChanged += OnItemSelectionChanged;
                _allFiles.Add(obj);
            }

            TotalCount = _allFiles.Count;
            LastRefreshTime = DateTime.Now.ToString("HH:mm:ss");
            ApplySearchFilter();
            RefreshSelectionState();

            _main.StatusMessage = _allFiles.Count > 0
                ? string.Empty
                : _main.LanguageService["msg_no_files"];
        }
        catch (Exception ex)
        {
            App.Log.Error("FileList operation failed", ex);
            _main.StatusMessage = OssExceptionHelper.GetFriendlyMessage(ex, _main.LanguageService);
        }
        finally
        {
            IsFileOperationBusy = false;
        }
    }

    // ──────────────────────── Search filter ────────────────────────

    private void ApplySearchFilter()
    {
        var q = SearchText?.Trim() ?? "";
        Files.Clear();

        foreach (var f in _allFiles)
        {
            if (MatchesFilter(f, q))
                Files.Add(f);
        }

        // Rebuild before pruning so nodes removed by a previous query come back.
        BuildTree();
        if (q.Length > 0 || ShowCleanableOnly)
            PruneTree(TreeNodes, q);

        // Both lists changed shape, so the checkbox states have to be re-derived.
        RefreshSelectionState();
    }

    /// <summary>
    /// Whether a node passes the search box and the cleanable-only filter.
    /// </summary>
    /// <remarks>
    /// A directory node has no age of its own, so it never matches the cleanable
    /// filter — it survives pruning through the children that do match. Its name is
    /// still matched against the search text, so typing "backend" keeps that whole
    /// subtree on screen.
    /// </remarks>
    private bool MatchesFilter(OssObjectItem node, string query)
    {
        var nameMatch = query.Length > 0 && node.Name.Contains(query, StringComparison.OrdinalIgnoreCase);

        if (node.IsFolder)
            return nameMatch;

        if (ShowCleanableOnly && !node.IsCleanable)
            return false;

        return query.Length == 0 || nameMatch || node.Key.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Builds <see cref="TreeNodes"/> from <see cref="_allFiles"/> by splitting each
    /// object's relative key on '/'. File nodes reuse the existing item instances so
    /// checkbox selection stays shared with the flat list; only directories are new.
    /// </summary>
    private void BuildTree()
    {
        TreeNodes.Clear();
        var directories = new Dictionary<string, OssObjectItem>(StringComparer.Ordinal);

        foreach (var file in _allFiles)
        {
            // Derive the relative path from Key, not Name: Name falls back to the full
            // key when an object sits exactly on the base prefix, which would nest the
            // browsing root inside itself as a phantom folder.
            var relative = file.Key;
            if (_basePrefix.Length > 0 && relative.StartsWith(_basePrefix, StringComparison.Ordinal))
                relative = relative[_basePrefix.Length..];

            var segments = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
                continue; // the base prefix itself — it is the root, not a child of it

            // A key ending in '/' is a directory placeholder created by the OSS
            // console — it becomes a directory node instead of a file row.
            var isPlaceholder = relative.EndsWith('/');
            var directoryDepth = isPlaceholder ? segments.Length : segments.Length - 1;

            OssObjectItem? parent = null;
            var path = string.Empty;
            for (var i = 0; i < directoryDepth; i++)
            {
                path += segments[i] + "/";
                if (!directories.TryGetValue(path, out var directory))
                {
                    directory = new OssObjectItem
                    {
                        Key = _basePrefix + path,
                        Name = path,
                        LeafName = segments[i],
                        IsFolder = true
                    };
                    // A directory's check state lives in the roll-up, but the click
                    // that sets it arrives as a normal property change.
                    directory.PropertyChanged += OnItemSelectionChanged;
                    directories[path] = directory;
                    (parent?.Children ?? TreeNodes).Add(directory);
                }
                parent = directory;
            }

            if (!isPlaceholder)
                (parent?.Children ?? TreeNodes).Add(file);
        }

        SortNodes(TreeNodes);
    }

    /// <summary>
    /// Orders each level of the tree: directories first, then by display name.
    /// </summary>
    private static void SortNodes(ObservableCollection<OssObjectItem> nodes)
    {
        var ordered = nodes
            .OrderByDescending(n => n.IsFolder)
            .ThenBy(n => n.LeafName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        nodes.Clear();
        foreach (var node in ordered)
        {
            SortNodes(node.Children);
            nodes.Add(node);
        }
    }

    /// <summary>
    /// Drops tree nodes that neither pass the active filters nor contain a matching
    /// descendant, and expands the ancestors of every hit so results stay visible in
    /// context. Runs against a freshly built tree.
    /// </summary>
    /// <param name="query">Trimmed search text; empty when only the cleanable filter is on.</param>
    /// <returns>Whether any node survived in this subtree.</returns>
    private bool PruneTree(ObservableCollection<OssObjectItem> nodes, string query)
    {
        var anyKept = false;

        // Snapshot — the collection is rewritten while iterating.
        foreach (var node in nodes.ToList())
        {
            var selfMatch = MatchesFilter(node, query);
            var descendantMatch = PruneTree(node.Children, query);

            if (selfMatch || descendantMatch)
            {
                node.IsExpanded = true;
                anyKept = true;
            }
            else
            {
                nodes.Remove(node);
            }
        }

        return anyKept;
    }

    // ──────────────────────── Selection ────────────────────────

    /// <summary>
    /// Toggles every row currently shown. Rows hidden by the search filter keep
    /// their state — "select all" means all of what the user can see.
    /// </summary>
    [RelayCommand]
    private void ToggleSelectAll()
    {
        var select = Files.Count > 0 && Files.Any(f => f.IsSelected != true);
        foreach (var file in Files)
            file.IsSelected = select;
        RefreshSelectionState();
    }

    /// <summary>
    /// Keeps the toolbar in step with the checkboxes. Ticking a row is a plain
    /// property change on <see cref="OssObjectItem"/>, so without this the counts
    /// behind <see cref="CanDelete"/> and <see cref="CanDownload"/> never move.
    /// </summary>
    private void OnItemSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_updatingSelection || e.PropertyName != nameof(OssObjectItem.IsSelected))
            return;

        // A directory's state is rolled up from its children, so a state arriving
        // here is a user click and has to be pushed down before the roll-up runs.
        // (null is written by the roll-up itself and means "some children".)
        if (sender is OssObjectItem { IsFolder: true } folder && folder.IsSelected != null)
        {
            _updatingSelection = true;
            try
            {
                ApplySelection(folder.Children, folder.IsSelected == true);
            }
            finally
            {
                _updatingSelection = false;
            }
        }

        RefreshSelectionState();
    }

    /// <summary>
    /// Applies a directory's check state to every file below it. Directory nodes are
    /// skipped: their own state is derived, so writing it here would be overwritten.
    /// </summary>
    private static void ApplySelection(IEnumerable<OssObjectItem> nodes, bool selected)
    {
        foreach (var node in nodes)
        {
            if (node.IsFolder)
                ApplySelection(node.Children, selected);
            else
                node.IsSelected = selected;
        }
    }

    /// <summary>
    /// Recomputes the directory roll-ups and the counters behind the toolbar buttons.
    /// </summary>
    private void RefreshSelectionState()
    {
        _updatingSelection = true;
        try
        {
            RollUpFolderStates(TreeNodes);

            SelectedCount = _allFiles.Count(f => f.IsSelected == true);

            var shown = Files.Count;
            var checkedShown = Files.Count(f => f.IsSelected == true);
            IsAllSelected = shown == 0 || checkedShown == 0
                ? false
                : checkedShown == shown
                    ? true
                    : (bool?)null;
        }
        finally
        {
            _updatingSelection = false;
        }
    }

    /// <summary>
    /// Propagates the check state of every directory node up from its files.
    /// </summary>
    /// <returns>
    /// The state of <paramref name="nodes"/> as a whole: <c>true</c> when every file
    /// below is checked, <c>false</c> when none is, <c>null</c> when only some are.
    /// </returns>
    private static bool? RollUpFolderStates(IList<OssObjectItem> nodes)
    {
        var anyChecked = false;
        var allChecked = true;

        foreach (var node in nodes)
        {
            var state = node.IsFolder ? RollUpFolderStates(node.Children) : node.IsSelected == true;

            if (node.IsFolder)
                node.IsSelected = state;

            if (state == true)
                anyChecked = true;
            else
                allChecked = false;
        }

        if (nodes.Count == 0)
            return false;

        return allChecked ? true : anyChecked ? null : false;
    }

    /// <summary>
    /// Unchecks everything. A check made in one view says nothing about the other —
    /// the flat list has no directory rows to roll up — so switching views starts
    /// from a clean sheet instead of a half-true selection.
    /// </summary>
    private void ClearSelection()
    {
        _updatingSelection = true;
        try
        {
            foreach (var file in _allFiles)
                file.IsSelected = false;

            foreach (var node in TreeNodes)
                ClearSelectionRecursive(node);
        }
        finally
        {
            _updatingSelection = false;
        }

        RefreshSelectionState();
    }

    private static void ClearSelectionRecursive(OssObjectItem node)
    {
        node.IsSelected = false;
        foreach (var child in node.Children)
            ClearSelectionRecursive(child);
    }

    /// <summary>
    /// The files a batch command acts on: every checked file, or the row selected in
    /// the grid or tree when nothing is checked. Checked rows win — they are the more
    /// explicit choice, and the count in the status bar refers to exactly them.
    /// </summary>
    private List<OssObjectItem> CollectTargets()
    {
        var checkedFiles = _allFiles.Where(f => f.IsSelected == true).ToList();
        if (checkedFiles.Count > 0)
            return checkedFiles;

        return SelectedFile != null ? [SelectedFile] : [];
    }

    // ──────────────────────── Notification Toast ────────────────────────

    /// <summary>
    /// Shows a temporary toast notification that auto-dismisses after <paramref name="durationMs"/>.
    /// Subsequent calls cancel and replace the previous notification.
    /// </summary>
    private async Task ShowNotificationAsync(string message, int durationMs = 2500)
    {
        _notificationCts?.Cancel();
        _notificationCts = new CancellationTokenSource();
        var token = _notificationCts.Token;

        NotificationMessage = message;
        IsNotificationVisible = true;

        try
        {
            await Task.Delay(durationMs, token);
        }
        catch (TaskCanceledException) { }

        IsNotificationVisible = false;
        NotificationMessage = string.Empty;
    }

    // ──────────────────────── View mode ────────────────────────

    /// <summary>
    /// Directory that uploads are written to. The tree view follows the selection —
    /// a directory node, or the parent directory of the selected file. The flat list
    /// has no directory context, so it keeps uploading to the base prefix as before.
    /// </summary>
    public string UploadPrefix
    {
        get
        {
            if (!IsTreeView || SelectedNode == null)
                return _basePrefix;

            return SelectedNode.IsFolder
                ? SelectedNode.Key
                : _basePrefix + ParentPath(SelectedNode.Name);
        }
    }

    partial void OnSelectedNodeChanged(OssObjectItem? value)
    {
        // Only real objects can be downloaded, deleted or previewed, so selecting a
        // directory clears the file selection instead of leaving a stale one behind.
        SelectedFile = value is { IsFolder: false } ? value : null;
    }

    /// <summary>
    /// Upload target rendered for the Upload button tooltip, so the destination is
    /// visible before the file picker opens. "/" stands for the bucket/prefix root.
    /// </summary>
    public string UploadTargetDisplay
    {
        get
        {
            var prefix = UploadPrefix;
            return _main.LanguageService["tooltip_upload_target"] + (prefix.Length > 0 ? prefix : "/");
        }
    }

    /// <summary>
    /// Flips between the list and tree views (toolbar button).
    /// </summary>
    /// <remarks>
    /// A plain command rather than a bound ToggleButton: the Fluent theme paints a
    /// checked ToggleButton with the accent background, which would swallow the
    /// accent-coloured glyph drawn on top of it.
    /// </remarks>
    [RelayCommand]
    private void ToggleView()
    {
        IsTreeView = !IsTreeView;
    }

    /// <summary>
    /// Applies the persisted view mode. Any value other than "list" selects the tree.
    /// </summary>
    public void ApplyViewMode(string? mode)
    {
        _suppressViewModePersist = true;
        try
        {
            IsTreeView = !string.Equals(mode, "list", StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            _suppressViewModePersist = false;
        }
    }

    partial void OnIsTreeViewChanged(bool value)
    {
        // The two views share the file instances but not the directory nodes, and the
        // rows on screen are not even the same set — drop the checks on every switch.
        ClearSelection();

        if (_suppressViewModePersist)
            return;

        _ = _main.PersistViewModeAsync(value ? "tree" : "list");
    }

    /// <summary>
    /// Returns the directory portion of a relative key, including the trailing
    /// separator ("backend/dev/app.log" → "backend/dev/").
    /// </summary>
    private static string ParentPath(string relativeName)
    {
        var slash = relativeName.LastIndexOf('/');
        return slash < 0 ? string.Empty : relativeName[..(slash + 1)];
    }

    // ──────────────────────── Cleanup flag ────────────────────────

    /// <summary>
    /// Applies the cleanup settings and re-evaluates the listing already on screen,
    /// so a change in the settings panel shows up without a reload.
    /// </summary>
    /// <param name="days">Age in days. 0 or less turns the flag off.</param>
    /// <param name="paths">
    /// Comma-separated directories the flag applies to, relative to the base prefix.
    /// Empty — the default — means every directory.
    /// </param>
    public void ApplyCleanableSettings(int days, string? paths)
    {
        _cleanableDays = days;
        _cleanableRoots = ParseCleanableRoots(paths);

        foreach (var file in _allFiles)
            UpdateCleanableFlag(file);

        // The marks changed, so a cleanable-only listing has to be rebuilt too.
        ApplySearchFilter();
    }

    /// <summary>
    /// Splits the configured directory list into path prefixes. Entries get a trailing
    /// slash so "backend" matches "backend/dev/app.log" without also matching
    /// "backend-old/app.log"; an empty list means every directory qualifies.
    /// </summary>
    private static string[] ParseCleanableRoots(string? paths)
    {
        if (string.IsNullOrWhiteSpace(paths))
            return [];

        var roots = new List<string>();
        foreach (var part in paths.Split(',',
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // OSS keys use '/', but a path pasted from Explorer should still work.
            var root = part.Replace('\\', '/').TrimStart('/');
            if (root.Length == 0)
                continue;

            roots.Add(root.EndsWith('/') ? root : root + "/");
        }

        return [.. roots];
    }

    /// <summary>
    /// Flags a real OSS object whose Last-Modified lies before the configured
    /// threshold and that sits in one of the configured directories. Directories are
    /// never flagged — neither tree nodes nor the placeholder objects the console
    /// creates for a folder, since clearing one is a directory decision, not an age
    /// decision. An unparsable timestamp also stays unflagged rather than reading as
    /// infinitely old.
    /// </summary>
    private void UpdateCleanableFlag(OssObjectItem item)
    {
        item.IsCleanable = _cleanableDays > 0
            && !item.IsFolder
            && !item.IsDirectoryPlaceholder
            && IsInCleanableScope(item.Name)
            && item.LastModified != DateTime.MinValue
            && item.LastModified < DateTime.UtcNow.AddDays(-_cleanableDays);
    }

    /// <summary>
    /// Whether the object's relative path sits under one of the configured cleanup
    /// directories.
    /// </summary>
    private bool IsInCleanableScope(string relativeName)
    {
        if (_cleanableRoots.Length == 0)
            return true;

        foreach (var root in _cleanableRoots)
        {
            if (relativeName.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    // ──────────────────────── Upload ────────────────────────

    [RelayCommand]
    private async Task UploadAsync()
    {
        if (string.IsNullOrEmpty(SelectedBucket))
        {
            _main.StatusMessage = _main.LanguageService["msg_no_bucket_selected"];
            return;
        }

        try
        {
            if (MainWindow == null) return;
            var window = MainWindow;

            var files = await window.StorageProvider.OpenFilePickerAsync(
                new Avalonia.Platform.Storage.FilePickerOpenOptions
                {
                    Title = _main.LanguageService["btn_upload"],
                    AllowMultiple = false
                });

            if (files.Count == 0) return;

            var localPath = files[0].Path.LocalPath;
            var objectName = UploadPrefix + Path.GetFileName(localPath);
            App.Log.Info($"Upload: local={localPath} → oss://{SelectedBucket}/{objectName}");

            IsFileOperationBusy = true;
            IsProgressVisible = true;
            _main.StatusMessage = _main.LanguageService["msg_uploading"];

            var progress = new Progress<double>(value =>
            {
                ProgressValue = value;
                IsProgressVisible = value < 100;
            });

            // Record the state of any same-named object before the PUT, so the log
            // shows exactly which key is being replaced, then verify the write
            // actually landed instead of trusting the 2xx response alone.
            var existing = await TryHeadAsync(SelectedBucket, objectName);
            if (existing != null)
                App.Log.Info($"Upload: replacing existing oss://{SelectedBucket}/{objectName} " +
                             $"(size={existing.Length}, etag={existing.ETag}, lastModified={existing.LastModified:O})");

            await _ossService.UploadFileAsync(SelectedBucket, objectName, localPath, progress);

            var applied = await IsUploadAppliedAsync(SelectedBucket, objectName, localPath, existing);
            var statusKey = applied ? "msg_upload_success" : "msg_upload_not_applied";
            _main.StatusMessage = _main.LanguageService[statusKey];
            _ = ShowNotificationAsync(_main.LanguageService[statusKey]);

            await LoadFilesAsync();
        }
        catch (Exception ex)
        {
            App.Log.Error("FileList operation failed", ex);
            _main.StatusMessage = OssExceptionHelper.GetFriendlyMessage(ex, _main.LanguageService);
        }
        finally
        {
            IsFileOperationBusy = false;
            IsProgressVisible = false;
        }
    }

    /// <summary>
    /// Starts an upload into the given directory node (tree view context menu).
    /// </summary>
    /// <param name="folder">Directory node to upload into.</param>
    [RelayCommand]
    private async Task UploadHereAsync(OssObjectItem? folder)
    {
        if (folder is not { IsFolder: true })
            return;

        // UploadPrefix follows the tree selection, so selecting the node is enough.
        SelectedNode = folder;
        await UploadAsync();
    }

    /// <summary>
    /// Copies the full key of a directory node to the clipboard.
    /// </summary>
    /// <param name="folder">Directory node whose key is copied.</param>
    [RelayCommand]
    private async Task CopyDirectoryNameAsync(OssObjectItem? folder)
    {
        if (folder is not { IsFolder: true })
            return;

        try
        {
            var topLevel = TopLevel.GetTopLevel(MainWindow);
            if (topLevel?.Clipboard != null)
                await topLevel.Clipboard.SetTextAsync(folder.Key);

            _main.StatusMessage = _main.LanguageService["msg_dir_path_copied"];
            _ = ShowNotificationAsync(_main.LanguageService["msg_dir_path_copied"]);
        }
        catch (Exception ex)
        {
            App.Log.Error("CopyDirectoryName failed", ex);
            _main.StatusMessage = OssExceptionHelper.GetFriendlyMessage(ex, _main.LanguageService);
        }
    }

    /// <summary>
    /// Reads the metadata of an object for the pre-upload probe, treating a failed
    /// probe as "unknown" so a transient network error never blocks an upload.
    /// </summary>
    private async Task<OssObjectHead?> TryHeadAsync(string bucket, string key)
    {
        try
        {
            return await _ossService.HeadObjectAsync(bucket, key);
        }
        catch (Exception ex)
        {
            App.Log.Info($"Upload: HEAD {key} failed, continuing without pre-check ({ex.Message})");
            return null;
        }
    }

    /// <summary>
    /// Confirms that the PUT reached the target object by comparing the local file
    /// size and the object's Last-Modified before and after the upload. OSS replaces
    /// same-named objects by default, so a write that did not move Last-Modified
    /// means the remote file was left untouched.
    /// </summary>
    /// <returns>
    /// <c>true</c> when the upload is confirmed <em>or</em> when the check is
    /// inconclusive — a probe failure must never turn a real upload into a false error.
    /// </returns>
    private async Task<bool> IsUploadAppliedAsync(string bucket, string key, string localPath,
        OssObjectHead? before)
    {
        try
        {
            var localSize = new FileInfo(localPath).Length;
            var after = await _ossService.HeadObjectAsync(bucket, key);

            if (after == null)
            {
                App.Log.Error($"Upload: oss://{bucket}/{key} is missing after a successful PUT");
                return false;
            }

            App.Log.Info($"Upload: oss://{bucket}/{key} is now size={after.Length}, " +
                         $"etag={after.ETag}, lastModified={after.LastModified:O}");

            if (after.Length != localSize)
            {
                App.Log.Error($"Upload: size mismatch after PUT (remote={after.Length}, local={localSize})");
                return false;
            }

            if (before != null && after.LastModified == before.LastModified)
            {
                App.Log.Error("Upload: Last-Modified unchanged, the object was not replaced");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            App.Log.Error("Upload verification inconclusive", ex);
            return true;
        }
    }

    /// <summary>
    /// Resolves the local path a download should be written to, mirroring the key's
    /// relative directory layout under <paramref name="targetFolder"/>.
    /// <see cref="OssObjectItem.Name"/> keeps the key's relative path (for example
    /// "backend/dev/app.log"), so the containing directories must be created first —
    /// otherwise the download fails with <see cref="DirectoryNotFoundException"/>.
    /// </summary>
    /// <returns>
    /// The absolute local path, or <c>null</c> when the name would escape
    /// <paramref name="targetFolder"/>.
    /// </returns>
    private static string? BuildDownloadPath(string targetFolder, string relativeName)
    {
        // OSS always uses '/' — normalize so a Windows-style key cannot smuggle
        // a drive-relative path into Path.Combine.
        var relative = relativeName.Replace('\\', '/').TrimStart('/');
        var full = Path.GetFullPath(Path.Combine(targetFolder, relative));

        var root = Path.GetFullPath(targetFolder)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            return null;

        var directory = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        return full;
    }

    // ──────────────────────── Download ────────────────────────

    [RelayCommand]
    private async Task DownloadAsync()
    {
        var targets = CollectTargets();
        if (targets.Count == 0)
        {
            _main.StatusMessage = _main.LanguageService["msg_select_file"];
            return;
        }

        try
        {
            if (MainWindow == null) return;
            var window = MainWindow;

            var folder = await window.StorageProvider.OpenFolderPickerAsync(
                new Avalonia.Platform.Storage.FolderPickerOpenOptions
                {
                    Title = _main.LanguageService["btn_download"],
                    AllowMultiple = false
                });

            if (folder.Count == 0) return;

            IsFileOperationBusy = true;
            IsProgressVisible = true;
            _main.StatusMessage = _main.LanguageService["msg_downloading"];

            var progress = new Progress<double>(value =>
            {
                ProgressValue = value;
                IsProgressVisible = value < 100;
            });

            var targetFolder = folder[0].Path.LocalPath;
            var downloaded = 0;
            Exception? firstError = null;

            foreach (var file in targets)
            {
                // Each file keeps its own relative path under the picked folder.
                var localPath = BuildDownloadPath(targetFolder, file.Name);
                if (localPath == null)
                {
                    App.Log.Error($"Download: {file.Name} resolves outside the target folder, skipped");
                    continue;
                }

                try
                {
                    App.Log.Info($"Download: oss://{SelectedBucket}/{file.Key} → {localPath}");
                    await _ossService.DownloadFileAsync(SelectedBucket!, file.Key, localPath, progress);
                    downloaded++;
                }
                catch (Exception ex)
                {
                    // One unreadable object must not abandon the rest of the batch.
                    firstError ??= ex;
                    App.Log.Error($"Download failed: oss://{SelectedBucket}/{file.Key}", ex);
                }
            }

            var message = downloaded == targets.Count
                ? targets.Count == 1
                    ? _main.LanguageService["msg_download_success"]
                    : string.Format(_main.LanguageService["msg_download_batch_success"], downloaded)
                : downloaded > 0
                    ? string.Format(_main.LanguageService["msg_download_batch_partial"], downloaded, targets.Count)
                    : firstError != null
                        ? OssExceptionHelper.GetFriendlyMessage(firstError, _main.LanguageService)
                        : _main.LanguageService["msg_download_path_invalid"];

            _main.StatusMessage = message;
            if (downloaded > 0)
                _ = ShowNotificationAsync(message);
        }
        catch (Exception ex)
        {
            App.Log.Error("FileList operation failed", ex);
            _main.StatusMessage = OssExceptionHelper.GetFriendlyMessage(ex, _main.LanguageService);
        }
        finally
        {
            IsFileOperationBusy = false;
            IsProgressVisible = false;
        }
    }

    // ──────────────────────── Batch Delete ────────────────────────

    [RelayCommand]
    private async Task DeleteAsync()
    {
        var toDelete = CollectTargets();

        if (toDelete.Count == 0)
        {
            _main.StatusMessage = _main.LanguageService["msg_select_file"];
            return;
        }

        if (ConfirmHandler != null)
        {
            var msg = toDelete.Count == 1
                ? _main.LanguageService["msg_delete_confirm"]
                : string.Format(_main.LanguageService["msg_delete_batch_confirm"], toDelete.Count);

            var confirmed = await ConfirmHandler(
                _main.LanguageService["msg_confirm_title"], msg);
            if (!confirmed) return;
        }

        try
        {
            IsFileOperationBusy = true;

            App.Log.Info($"Delete: {toDelete.Count} file(s) from bucket={SelectedBucket}");
            foreach (var file in toDelete)
            {
                App.Log.Info($"  Deleting: oss://{SelectedBucket}/{file.Key}");
                await _ossService.DeleteFileAsync(SelectedBucket!, file.Key);
            }

            var deleteMsg = toDelete.Count == 1
                ? _main.LanguageService["msg_delete_success"]
                : string.Format(_main.LanguageService["msg_delete_batch_success"], toDelete.Count);
            _main.StatusMessage = deleteMsg;
            _ = ShowNotificationAsync(deleteMsg);

            await LoadFilesAsync();
        }
        catch (Exception ex)
        {
            App.Log.Error("FileList operation failed", ex);
            _main.StatusMessage = OssExceptionHelper.GetFriendlyMessage(ex, _main.LanguageService);
        }
        finally
        {
            IsFileOperationBusy = false;
        }
    }

    // ──────────────────────── Pre-signed URL ────────────────────────

    [RelayCommand]
    private async Task GenerateSignedUrlAsync()
    {
        if (SelectedFile == null)
        {
            _main.StatusMessage = _main.LanguageService["msg_select_file"];
            return;
        }

        try
        {
            var url = await Task.Run(() =>
                _ossService.GeneratePresignedUrl(SelectedBucket!, SelectedFile.Key));
            App.Log.Info($"GenerateSignedUrl: oss://{SelectedBucket}/{SelectedFile.Key}");

            var topLevel = TopLevel.GetTopLevel(MainWindow);
            if (topLevel?.Clipboard != null)
                await topLevel.Clipboard.SetTextAsync(url);

            _main.StatusMessage = _main.LanguageService["msg_url_copied"];
            _ = ShowNotificationAsync(_main.LanguageService["msg_url_copied"]);
        }
        catch (Exception ex)
        {
            App.Log.Error("FileList operation failed", ex);
            _main.StatusMessage = OssExceptionHelper.GetFriendlyMessage(ex, _main.LanguageService);
        }
    }

    // ──────────────────────── File Classification ────────────────────────

    /// <summary>
    /// Supported image file extensions for preview.
    /// </summary>
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".ico"
    };

    /// <summary>
    /// Supported text file extensions for preview/edit/copy-content.
    /// </summary>
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".json", ".xml", ".md", ".csv", ".log",
        ".cs", ".py", ".js", ".ts", ".html", ".css",
        ".yaml", ".yml", ".ini", ".cfg", ".sh", ".bat",
        ".csproj", ".sln", ".props", ".targets", ".razor", ".xaml", ".axaml"
    };

    /// <summary>
    /// Classifies a file by extension, returning (canPreview, isTextFile).
    /// </summary>
    private static (bool canPreview, bool isTextFile) ClassifyFile(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(ext)) return (false, false);

        var isImage = ImageExtensions.Contains(ext);
        var isText = TextExtensions.Contains(ext);
        return (isImage || isText, isText);
    }

    // ──────────────────────── Preview ────────────────────────

    /// <summary>
    /// Preview the file passed from the row-level button or context menu.
    /// </summary>
    [RelayCommand]
    private async Task PreviewFileAsync(OssObjectItem? file)
    {
        if (file == null) return;
        await DoPreviewAsync(file);
    }

    [RelayCommand]
    private async Task PreviewAsync()
    {
        if (SelectedFile == null)
        {
            _main.StatusMessage = _main.LanguageService["msg_select_file"];
            return;
        }
        await DoPreviewAsync(SelectedFile);
    }

    private async Task DoPreviewAsync(OssObjectItem file)
    {
        try
        {
            IsFileOperationBusy = true;
            _main.StatusMessage = _main.LanguageService["msg_preview_loading"];

            var ext = Path.GetExtension(file.Name).ToLowerInvariant();
            var isImage = ImageExtensions.Contains(ext);
            var isText = TextExtensions.Contains(ext);

            var tmpPath = Path.Combine(Path.GetTempPath(), $"oss_preview_{Guid.NewGuid():N}{ext}");
            await _ossService.DownloadFileAsync(SelectedBucket!, file.Key, tmpPath);

            if (isImage)
            {
                var win = new Views.PreviewWindow(tmpPath, file.Name, isImage: true);
                if (MainWindow != null) await win.ShowDialog(MainWindow);
            }
            else if (isText)
            {
                var text = await File.ReadAllTextAsync(tmpPath);
                if (text.Length > 500_000)
                    text = text[..500_000] + "\n\n... [truncated]";
                var win = new Views.PreviewWindow(text, file.Name, isImage: false);
                if (MainWindow != null) await win.ShowDialog(MainWindow);
            }
            else
            {
                var info = $"File: {file.Name}\n" +
                           $"Size: {file.SizeDisplay}\n" +
                           $"Type: {ext.TrimStart('.')?.ToUpper() ?? "Unknown"}\n" +
                           $"Modified: {file.LastModifiedDisplay}\n\n" +
                           $"{_main.LanguageService["msg_preview_unsupported"]}";
                var win = new Views.PreviewWindow(info, file.Name, isImage: false);
                if (MainWindow != null) await win.ShowDialog(MainWindow);
            }

            try { File.Delete(tmpPath); } catch { }

            _main.StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            App.Log.Error("FileList operation failed", ex);
            _main.StatusMessage = OssExceptionHelper.GetFriendlyMessage(ex, _main.LanguageService);
        }
        finally
        {
            IsFileOperationBusy = false;
        }
    }

    // ──────────────────────── Edit (context menu) ────────────────────────

    /// <summary>
    /// Downloads a text file, opens an editable window, and uploads the
    /// modified content back to OSS when the user clicks Save.
    /// Only available for text-type files.
    /// </summary>
    [RelayCommand]
    private async Task EditFileAsync(OssObjectItem? file)
    {
        if (file == null) return;

        try
        {
            IsFileOperationBusy = true;
            _main.StatusMessage = _main.LanguageService["msg_preview_loading"];

            var ext = Path.GetExtension(file.Name);
            var tmpPath = Path.Combine(Path.GetTempPath(), $"oss_edit_{Guid.NewGuid():N}{ext}");
            await _ossService.DownloadFileAsync(SelectedBucket!, file.Key, tmpPath);

            var text = await File.ReadAllTextAsync(tmpPath);
            if (text.Length > 1_000_000)
                text = text[..1_000_000];

            var editWindow = new Views.EditWindow(text, file.Name);
            if (MainWindow != null)
                await editWindow.ShowDialog(MainWindow);

            if (editWindow.Saved)
            {
                _main.StatusMessage = _main.LanguageService["msg_uploading"];
                var newText = editWindow.EditedText;
                await File.WriteAllTextAsync(tmpPath, newText);

                var progress = new Progress<double>(value =>
                {
                    ProgressValue = value;
                    IsProgressVisible = value < 100;
                });
                IsProgressVisible = true;

                await _ossService.UploadFileAsync(SelectedBucket!, file.Key, tmpPath, progress);

                _main.StatusMessage = _main.LanguageService["msg_edit_success"];
                _ = ShowNotificationAsync(_main.LanguageService["msg_edit_success"]);
                await LoadFilesAsync();
            }
            else
            {
                _main.StatusMessage = string.Empty;
            }

            try { File.Delete(tmpPath); } catch { }
        }
        catch (Exception ex)
        {
            App.Log.Error("EditFile failed", ex);
            _main.StatusMessage = OssExceptionHelper.GetFriendlyMessage(ex, _main.LanguageService);
        }
        finally
        {
            IsFileOperationBusy = false;
            IsProgressVisible = false;
        }
    }

    // ──────────────────────── Copy Content ────────────────────────

    /// <summary>
    /// Downloads a text file and copies its content to the clipboard.
    /// Only available for text-type files.
    /// </summary>
    [RelayCommand]
    private async Task CopyContentAsync(OssObjectItem? file)
    {
        if (file == null) return;

        try
        {
            if (string.IsNullOrEmpty(SelectedBucket)) return;

            IsFileOperationBusy = true;
            _main.StatusMessage = _main.LanguageService["msg_preview_loading"];

            var ext = Path.GetExtension(file.Name);
            var tmpPath = Path.Combine(Path.GetTempPath(), $"oss_copy_{Guid.NewGuid():N}{ext}");
            await _ossService.DownloadFileAsync(SelectedBucket, file.Key, tmpPath);

            var text = await File.ReadAllTextAsync(tmpPath);
            if (text.Length > 1_000_000)
                text = text[..1_000_000];

            var topLevel = TopLevel.GetTopLevel(MainWindow);
            if (topLevel?.Clipboard != null)
                await topLevel.Clipboard.SetTextAsync(text);

            try { File.Delete(tmpPath); } catch { }

            _main.StatusMessage = _main.LanguageService["msg_content_copied"];
            _ = ShowNotificationAsync(_main.LanguageService["msg_content_copied"]);
        }
        catch (Exception ex)
        {
            App.Log.Error("CopyContent failed", ex);
            _main.StatusMessage = OssExceptionHelper.GetFriendlyMessage(ex, _main.LanguageService);
        }
        finally
        {
            IsFileOperationBusy = false;
        }
    }

    // ──────────────────────── Copy File Name ────────────────────────

    /// <summary>
    /// Copies the display name of the file to the clipboard.
    /// </summary>
    [RelayCommand]
    private async Task CopyFileNameAsync(OssObjectItem? file)
    {
        if (file == null) return;

        try
        {
            var topLevel = TopLevel.GetTopLevel(MainWindow);
            if (topLevel?.Clipboard != null)
                await topLevel.Clipboard.SetTextAsync(file.Name);

            _main.StatusMessage = _main.LanguageService["msg_file_name_copied"];
            _ = ShowNotificationAsync(_main.LanguageService["msg_file_name_copied"]);
        }
        catch (Exception ex)
        {
            App.Log.Error("CopyFileName failed", ex);
            _main.StatusMessage = OssExceptionHelper.GetFriendlyMessage(ex, _main.LanguageService);
        }
    }

    // ──────────────────────── Copy File Link ────────────────────────

    /// <summary>
    /// Generates a pre-signed URL for the file and copies it to the clipboard.
    /// </summary>
    [RelayCommand]
    private async Task CopyFileLinkAsync(OssObjectItem? file)
    {
        if (file == null) return;

        try
        {
            if (string.IsNullOrEmpty(SelectedBucket)) return;

            var url = await Task.Run(() =>
                _ossService.GeneratePresignedUrl(SelectedBucket, file.Key));
            App.Log.Info($"CopyFileLink: oss://{SelectedBucket}/{file.Key}");

            var topLevel = TopLevel.GetTopLevel(MainWindow);
            if (topLevel?.Clipboard != null)
                await topLevel.Clipboard.SetTextAsync(url);

            _main.StatusMessage = _main.LanguageService["msg_file_link_copied"];
            _ = ShowNotificationAsync(_main.LanguageService["msg_file_link_copied"]);
        }
        catch (Exception ex)
        {
            App.Log.Error("CopyFileLink failed", ex);
            _main.StatusMessage = OssExceptionHelper.GetFriendlyMessage(ex, _main.LanguageService);
        }
    }

    // ──────────────────────── Copy File Info ────────────────────────

    /// <summary>
    /// Copies file metadata (name, key, size, last modified) to the clipboard.
    /// </summary>
    [RelayCommand]
    private async Task CopyFileInfoAsync(OssObjectItem? file)
    {
        if (file == null) return;

        try
        {
            var info = $"Name: {file.Name}\n" +
                       $"Key: {file.Key}\n" +
                       $"Size: {file.SizeDisplay}\n" +
                       $"Last Modified: {file.LastModifiedDisplay}";

            var topLevel = TopLevel.GetTopLevel(MainWindow);
            if (topLevel?.Clipboard != null)
                await topLevel.Clipboard.SetTextAsync(info);

            _main.StatusMessage = _main.LanguageService["msg_file_info_copied"];
            _ = ShowNotificationAsync(_main.LanguageService["msg_file_info_copied"]);
        }
        catch (Exception ex)
        {
            App.Log.Error("CopyFileInfo failed", ex);
            _main.StatusMessage = OssExceptionHelper.GetFriendlyMessage(ex, _main.LanguageService);
        }
    }

    // ──────────────────────── Download (single file, context menu) ────────────────────────

    /// <summary>
    /// Downloads a single file via the context menu (no batch selection needed).
    /// </summary>
    [RelayCommand]
    private async Task DownloadFileAsync(OssObjectItem? file)
    {
        if (file == null) return;

        try
        {
            if (MainWindow == null) return;
            var window = MainWindow;

            var folder = await window.StorageProvider.OpenFolderPickerAsync(
                new Avalonia.Platform.Storage.FolderPickerOpenOptions
                {
                    Title = _main.LanguageService["btn_download"],
                    AllowMultiple = false
                });

            if (folder.Count == 0) return;

            var localPath = BuildDownloadPath(folder[0].Path.LocalPath, file.Name);
            if (localPath == null)
            {
                _main.StatusMessage = _main.LanguageService["msg_download_path_invalid"];
                return;
            }
            App.Log.Info($"Download (context): oss://{SelectedBucket}/{file.Key} → {localPath}");

            IsFileOperationBusy = true;
            IsProgressVisible = true;
            _main.StatusMessage = _main.LanguageService["msg_downloading"];

            var progress = new Progress<double>(value =>
            {
                ProgressValue = value;
                IsProgressVisible = value < 100;
            });

            await _ossService.DownloadFileAsync(SelectedBucket!, file.Key, localPath, progress);

            _main.StatusMessage = _main.LanguageService["msg_download_success"];
            _ = ShowNotificationAsync(_main.LanguageService["msg_download_success"]);
        }
        catch (Exception ex)
        {
            App.Log.Error("DownloadFile failed", ex);
            _main.StatusMessage = OssExceptionHelper.GetFriendlyMessage(ex, _main.LanguageService);
        }
        finally
        {
            IsFileOperationBusy = false;
            IsProgressVisible = false;
        }
    }

    // ──────────────────────── Delete (single file, context menu) ────────────────────────

    /// <summary>
    /// Deletes a single file via the context menu (with confirmation).
    /// </summary>
    [RelayCommand]
    private async Task DeleteFileAsync(OssObjectItem? file)
    {
        if (file == null) return;

        if (ConfirmHandler != null)
        {
            var confirmed = await ConfirmHandler(
                _main.LanguageService["msg_confirm_title"],
                _main.LanguageService["msg_delete_confirm"]);
            if (!confirmed) return;
        }

        try
        {
            IsFileOperationBusy = true;

            App.Log.Info($"Delete (context): oss://{SelectedBucket}/{file.Key}");
            await _ossService.DeleteFileAsync(SelectedBucket!, file.Key);

            _main.StatusMessage = _main.LanguageService["msg_delete_success"];
            _ = ShowNotificationAsync(_main.LanguageService["msg_delete_success"]);

            await LoadFilesAsync();
        }
        catch (Exception ex)
        {
            App.Log.Error("DeleteFile failed", ex);
            _main.StatusMessage = OssExceptionHelper.GetFriendlyMessage(ex, _main.LanguageService);
        }
        finally
        {
            IsFileOperationBusy = false;
        }
    }
}
