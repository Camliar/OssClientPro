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
    public void NavigateToSettings()
    {
        ShowSettings = true;
        ShowMainContent = false;
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
                return;
            }

            _ossService.Initialize(config);

            // Apply the configured base prefix for file filtering
            FileList.SetBasePrefix(config.BasePrefix);

            FileList.Buckets.Clear();

            if (!string.IsNullOrEmpty(config.DefaultBucket))
            {
                // When a default bucket is configured, use it directly.
                // Skip ListBucketsAsync() to avoid permission errors — the
                // account may only have access to this specific bucket.
                FileList.Buckets.Add(config.DefaultBucket);
                FileList.SelectedBucket = config.DefaultBucket;
            }
            else
            {
                // No default bucket: list all buckets so the user can pick one
                var buckets = await _ossService.ListBucketsAsync();
                foreach (var bucket in buckets)
                {
                    FileList.Buckets.Add(bucket.Name);
                }
            }

            IsConnected = true;
            StatusMessage = string.Empty;
            NavigateToMain();
        }
        catch (Exception ex)
        {
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
