using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OssClientPro.Helpers;
using OssClientPro.Services;

namespace OssClientPro.ViewModels;

/// <summary>
/// ViewModel for the main window. Orchestrates bucket listing, file operations,
/// settings visibility, and top-level commands.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly ConfigService _configService;
    private readonly OssService _ossService;

    public LanguageService LanguageService { get; }

    public SettingsViewModel Settings { get; }
    public FileListViewModel FileList { get; }

    /// <summary>
    /// Whether the settings panel is currently shown.
    /// </summary>
    [ObservableProperty]
    public partial bool ShowSettings { get; set; }

    /// <summary>
    /// Whether the main content (buckets + files) is currently shown.
    /// </summary>
    [ObservableProperty]
    public partial bool ShowMainContent { get; set; }

    /// <summary>
    /// Whether the client is connected and authenticated.
    /// </summary>
    [ObservableProperty]
    public partial bool IsConnected { get; set; }

    /// <summary>
    /// Current status message displayed in the status bar.
    /// </summary>
    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    /// <summary>
    /// Whether a long-running operation is in progress.
    /// </summary>
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>
    /// Current progress value (0.0–100.0).
    /// </summary>
    [ObservableProperty]
    public partial double ProgressValue { get; set; }

    /// <summary>
    /// Whether the progress bar is visible.
    /// </summary>
    [ObservableProperty]
    public partial bool IsProgressVisible { get; set; }

    public MainViewModel() : this(new ConfigService(), new OssService(), null!) { }

    public MainViewModel(ConfigService configService, OssService ossService, LanguageService languageService)
    {
        _configService = configService;
        _ossService = ossService;
        LanguageService = languageService;

        Settings = new SettingsViewModel(configService, this);
        FileList = new FileListViewModel(ossService, this);

        // Default to showing settings first so the user can enter credentials
        ShowSettings = true;
        ShowMainContent = false;
    }

    /// <summary>
    /// Navigates back to the main file-management view.
    /// </summary>
    public void NavigateToMain()
    {
        ShowSettings = false;
        ShowMainContent = true;
    }

    /// <summary>
    /// Navigates to the settings view.
    /// </summary>
    [RelayCommand]
    private void OpenSettings()
    {
        NavigateToSettings();
    }

    /// <summary>
    /// Navigates to the settings view.
    /// </summary>
    /// <summary>
    /// Called after initialization. If a valid config exists, auto-connect
    /// and skip the settings page. Otherwise show settings for first-time setup.
    /// </summary>
    public async Task StartupAsync()
    {
        var config = await _configService.LoadConfigAsync();
        if (config.IsValid)
        {
            await ConnectAsync();
        }
    }

    public void NavigateToSettings()
    {
        ShowSettings = true;
        ShowMainContent = false;
    }

    /// <summary>
    /// Persists the file-area view mode chosen in the UI.
    /// </summary>
    /// <param name="mode">"tree" or "list".</param>
    /// <remarks>
    /// The config is reloaded first: <see cref="ConfigService.SaveConfigAsync"/> takes
    /// plaintext credentials and re-encrypts them, so it needs the full loaded config
    /// rather than the encrypted values already on disk.
    /// </remarks>
    public async Task PersistViewModeAsync(string mode)
    {
        try
        {
            var config = await _configService.LoadConfigAsync();
            if (string.Equals(config.ViewMode, mode, StringComparison.OrdinalIgnoreCase))
                return;

            config.ViewMode = mode;
            await _configService.SaveConfigAsync(config);
            App.Log.Info($"View mode changed: {mode}");
        }
        catch (Exception ex)
        {
            App.Log.Error("Failed to persist view mode", ex);
        }
    }

    /// <summary>
    /// Connects to OSS using the saved configuration and loads buckets.
    /// </summary>
    public async Task ConnectAsync()
    {
        StatusMessage = LanguageService["msg_connecting"];
        IsBusy = true;

        try
        {
            var config = await _configService.LoadConfigAsync();
            if (!config.IsValid)
            {
                StatusMessage = LanguageService["msg_invalid_config"];
                App.Log.Warn("Connect skipped — invalid config");
                return;
            }

            App.Log.Info($"Connecting to {config.Endpoint}, region={config.Region}, bucket={config.DefaultBucket}, prefix={config.BasePrefix}");
            _ossService.Initialize(config);

            FileList.SetBasePrefix(config.BasePrefix);
            FileList.ApplyViewMode(config.ViewMode);
            // Before the bucket is picked: that assignment is what triggers the load
            // which stamps the cleanup flag onto every object.
            FileList.ApplyCleanableSettings(config.CleanableDays, config.CleanablePaths);
            FileList.Buckets.Clear();

            if (!string.IsNullOrEmpty(config.DefaultBucket))
            {
                FileList.Buckets.Add(config.DefaultBucket);
                FileList.SelectedBucket = config.DefaultBucket;
                App.Log.Info($"Using default bucket: {config.DefaultBucket}");
            }
            else
            {
                var buckets = await _ossService.ListBucketsAsync();
                foreach (var bucket in buckets)
                    FileList.Buckets.Add(bucket);
                App.Log.Info($"Listed {FileList.Buckets.Count} buckets");
            }

            IsConnected = true;
            StatusMessage = string.Empty;
            NavigateToMain();
            App.Log.Info("Connect succeeded");
        }
        catch (Exception ex)
        {
            App.Log.Error("Connect failed", ex);
            StatusMessage = OssExceptionHelper.GetFriendlyMessage(ex, LanguageService);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Reports operation progress to the UI.
    /// </summary>
    public void ReportProgress(double value)
    {
        ProgressValue = value;
        IsProgressVisible = value > 0 && value < 100;
    }
}
