using System.Collections.ObjectModel;
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

    [ObservableProperty]
    public partial string? SelectedBucket { get; set; }

    [ObservableProperty]
    public partial OssObjectItem? SelectedFile { get; set; }

    [ObservableProperty]
    public partial double ProgressValue { get; set; }

    [ObservableProperty]
    public partial bool IsProgressVisible { get; set; }

    [ObservableProperty]
    public partial bool IsFileOperationBusy { get; set; }

    [ObservableProperty]
    public partial bool IsAllSelected { get; set; }

    [ObservableProperty]
    public partial int SelectedCount { get; set; }

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

    private string _basePrefix = string.Empty;
    private readonly List<OssObjectItem> _allFiles = [];

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
                obj.SizeDisplay = _main.LanguageService.FormatFileSize(obj.Size);
                obj.LastModifiedDisplay = _main.LanguageService.FormatDateTime(obj.LastModified);
                obj.IsSelected = false;
                (obj.CanPreview, obj.IsTextFile) = ClassifyFile(obj.Name);
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
            if (q.Length == 0 ||
                f.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                f.Key.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                Files.Add(f);
            }
        }
    }

    // ──────────────────────── Select-All ────────────────────────

    [RelayCommand]
    private void ToggleSelectAll()
    {
        var select = Files.Count > 0 && SelectedCount < Files.Count;
        foreach (var file in Files)
            file.IsSelected = select;
        RefreshSelectionState();
    }

    private void RefreshSelectionState()
    {
        var count = Files.Count(f => f.IsSelected);
        SelectedCount = count;
        IsAllSelected = count > 0 && count == Files.Count;
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
            var objectName = _basePrefix + Path.GetFileName(localPath);
            App.Log.Info($"Upload: local={localPath} → oss://{SelectedBucket}/{objectName}");

            IsFileOperationBusy = true;
            IsProgressVisible = true;
            _main.StatusMessage = _main.LanguageService["msg_uploading"];

            var progress = new Progress<double>(value =>
            {
                ProgressValue = value;
                IsProgressVisible = value < 100;
            });

            await _ossService.UploadFileAsync(SelectedBucket, objectName, localPath, progress);

            _main.StatusMessage = _main.LanguageService["msg_upload_success"];
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

    // ──────────────────────── Download ────────────────────────

    [RelayCommand]
    private async Task DownloadAsync()
    {
        if (SelectedFile == null)
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

            var localPath = Path.Combine(folder[0].Path.LocalPath, SelectedFile.Name);
            App.Log.Info($"Download: oss://{SelectedBucket}/{SelectedFile.Key} → {localPath}");

            IsFileOperationBusy = true;
            IsProgressVisible = true;
            _main.StatusMessage = _main.LanguageService["msg_downloading"];

            var progress = new Progress<double>(value =>
            {
                ProgressValue = value;
                IsProgressVisible = value < 100;
            });

            await _ossService.DownloadFileAsync(SelectedBucket!, SelectedFile.Key, localPath, progress);

            _main.StatusMessage = _main.LanguageService["msg_download_success"];
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
        var toDelete = Files.Where(f => f.IsSelected).ToList();
        if (toDelete.Count == 0 && SelectedFile != null)
            toDelete = [SelectedFile];

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

            _main.StatusMessage = toDelete.Count == 1
                ? _main.LanguageService["msg_delete_success"]
                : string.Format(_main.LanguageService["msg_delete_batch_success"], toDelete.Count);

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
    /// Opens a text file for viewing/editing. Only available for text-type files.
    /// </summary>
    [RelayCommand]
    private async Task EditFileAsync(OssObjectItem? file)
    {
        if (file == null) return;
        // Edit reuses the Preview window for now; future enhancement could add
        // an editable text area with save-back-to-OSS capability.
        await DoPreviewAsync(file);
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

            var localPath = Path.Combine(folder[0].Path.LocalPath, file.Name);
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
