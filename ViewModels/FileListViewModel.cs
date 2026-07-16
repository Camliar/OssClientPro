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
}
