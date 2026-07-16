using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OssClientPro.Models;
using OssClientPro.Services;

namespace OssClientPro.ViewModels;

/// <summary>
/// ViewModel for the settings page. Manages OSS credentials input,
/// language selection, and configuration persistence.
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly ConfigService _configService;
    private readonly MainViewModel _main;

    [ObservableProperty]
    public partial string EditEndpoint { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditAccessKeyId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditAccessKeySecret { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditRegion { get; set; } = string.Empty;

    /// <summary>
    /// Default bucket to auto-select after connecting.
    /// Leave empty to pick from bucket list manually.
    /// </summary>
    [ObservableProperty]
    public partial string EditDefaultBucket { get; set; } = string.Empty;

    /// <summary>
    /// Optional file directory prefix within the bucket.
    /// Only objects under this path are shown in the file list.
    /// </summary>
    [ObservableProperty]
    public partial string EditBasePrefix { get; set; } = string.Empty;

    /// <summary>
    /// The currently selected language culture name (e.g. "zh-CN").
    /// </summary>
    [ObservableProperty]
    public partial string SelectedLanguage { get; set; } = LanguageService.DefaultCulture;

    /// <summary>
    /// Available languages for the dropdown.
    /// </summary>
    public ObservableCollection<string> AvailableLanguages { get; } =
    [
        "zh-CN",
        "en-US"
    ];

    /// <summary>
    /// Feedback message shown after save.
    /// </summary>
    [ObservableProperty]
    public partial string SaveMessage { get; set; } = string.Empty;

    /// <summary>
    /// Whether the save was successful (used to style the message).
    /// </summary>
    [ObservableProperty]
    public partial bool IsSaveSuccess { get; set; }

    public SettingsViewModel() : this(new ConfigService(), null!) { }

    public SettingsViewModel(ConfigService configService, MainViewModel main)
    {
        _configService = configService;
        _main = main;
    }

    /// <summary>
    /// Loads existing configuration and populates the form fields.
    /// </summary>
    public async Task LoadConfigAsync()
    {
        var config = await _configService.LoadConfigAsync();
        ApplyConfig(config);
    }

    /// <summary>
    /// Synchronously applies a loaded <see cref="OssConfig"/> to the form fields.
    /// Used during startup to avoid async complexity.
    /// </summary>
    public void LoadConfig(OssConfig? config)
    {
        ApplyConfig(config ?? new OssConfig());
    }

    /// <summary>
    /// Applies the config values to the observable properties.
    /// </summary>
    private void ApplyConfig(OssConfig config)
    {
        EditEndpoint = config.Endpoint;
        EditAccessKeyId = config.AccessKeyId;
        EditAccessKeySecret = config.AccessKeySecret;
        EditRegion = config.Region;
        EditDefaultBucket = config.DefaultBucket;
        EditBasePrefix = config.BasePrefix;

        if (!string.IsNullOrEmpty(config.Language))
            SelectedLanguage = config.Language;
    }

    /// <summary>
    /// Saves the current configuration to config.json and applies language changes.
    /// </summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(EditEndpoint) ||
            string.IsNullOrWhiteSpace(EditAccessKeyId) ||
            string.IsNullOrWhiteSpace(EditAccessKeySecret))
        {
            SaveMessage = _main?.LanguageService?["msg_invalid_config"] ?? "Invalid configuration.";
            IsSaveSuccess = false;
            return;
        }

        var config = new OssConfig
        {
            Endpoint = EditEndpoint.Trim(),
            AccessKeyId = EditAccessKeyId.Trim(),
            AccessKeySecret = EditAccessKeySecret.Trim(),
            Region = EditRegion.Trim(),
            DefaultBucket = EditDefaultBucket.Trim(),
            BasePrefix = EditBasePrefix.Trim(),
            Language = SelectedLanguage
        };

        await _configService.SaveConfigAsync(config);

        // Apply language change immediately
        _main?.LanguageService.SetLanguage(SelectedLanguage);

        SaveMessage = _main?.LanguageService?["msg_config_saved"] ?? "Configuration saved successfully.";
        IsSaveSuccess = true;

        // Auto-connect after saving
        if (_main != null)
            await _main.ConnectAsync();
    }

    /// <summary>
    /// Cancels settings editing and navigates back to the main view.
    /// Only available when already connected (i.e., editing existing config).
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        _main?.NavigateToMain();
    }

    /// <summary>
    /// Handles language selection change from the dropdown.
    /// </summary>
    partial void OnSelectedLanguageChanged(string value)
    {
        if (!string.IsNullOrEmpty(value) && _main != null)
        {
            _main.LanguageService.SetLanguage(value);
        }
    }
}
