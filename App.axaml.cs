using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using OssClientPro.Models;
using OssClientPro.Services;
using OssClientPro.ViewModels;
using OssClientPro.Views;

namespace OssClientPro;

public partial class App : Application
{
    private LanguageService? _languageService;
    internal static LogService Log { get; } = new();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            try
            {
                InitializeApp(desktop);
            }
            catch (Exception ex)
            {
                Log.Error("FATAL startup error", ex);
                Debug.WriteLine($"[OssClientPro] FATAL startup error: {ex}");
                throw;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void InitializeApp(IClassicDesktopStyleApplicationLifetime desktop)
    {
        Log.InitSession();
        Log.Info("=== InitializeApp START ===");

        var languagesPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Languages");
        Log.Info($"Languages path: {languagesPath}");

        var configService = new ConfigService();
        var ossService = new OssService();
        Log.Info("Services created");

        var config = LoadConfigSync(configService);
        var initialLanguage = !string.IsNullOrEmpty(config.Language)
            ? config.Language
            : LanguageService.DetectSystemLanguage();
        Log.Info($"Initial language: {initialLanguage}");

        _languageService = new LanguageService(languagesPath);
        _languageService.SetLanguage(initialLanguage);
        Resources["Localization"] = _languageService;
        Log.Info("LanguageService initialized");

        var mainViewModel = new MainViewModel(configService, ossService, _languageService);
        mainViewModel.Settings.LoadConfig(config);

        // If valid config exists, auto-connect → skip settings page.
        // If config is invalid/empty, stay on settings for first-time setup.
        mainViewModel.ShowSettings = !config.IsValid;
        mainViewModel.ShowMainContent = config.IsValid;
        Log.Info($"MainViewModel created, config valid={config.IsValid}");

        var mainWindow = new MainWindow { DataContext = mainViewModel };
        Log.Info("MainWindow created");

        // Auto-connect in background (won't block UI)
        if (config.IsValid)
            _ = mainViewModel.StartupAsync();

        mainViewModel.FileList.ConfirmHandler = async (title, message) =>
        {
            var result = false;
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                result = await ShowConfirmDialog(mainWindow, title, message);
            });
            return result;
        };

        mainViewModel.FileList.MainWindow = mainWindow;
        desktop.MainWindow = mainWindow;
        Log.Info("MainWindow set. === INIT COMPLETE ===");
    }

    private static OssConfig LoadConfigSync(ConfigService configService)
    {
        return configService.LoadConfigAsync().GetAwaiter().GetResult();
    }

    private async Task<bool> ShowConfirmDialog(Window owner, string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 420,
            Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            ShowInTaskbar = false
        };

        var confirmed = false;

        var grid = new Grid
        {
            Margin = new Thickness(20),
            RowDefinitions = new RowDefinitions("*,Auto")
        };

        var msgBlock = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(msgBlock, 0);
        grid.Children.Add(msgBlock);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 16, 0, 0)
        };

        var cancelBtn = new Button { Content = _languageService?["btn_cancel"] ?? "Cancel" };
        cancelBtn.Click += (_, _) => dialog.Close();

        var confirmBtn = new Button
        {
            Content = _languageService?["btn_delete"] ?? "Delete",
            Foreground = Brushes.White,
            Background = Brushes.IndianRed
        };
        confirmBtn.Click += (_, _) => { confirmed = true; dialog.Close(); };

        buttonPanel.Children.Add(cancelBtn);
        buttonPanel.Children.Add(confirmBtn);
        Grid.SetRow(buttonPanel, 1);
        grid.Children.Add(buttonPanel);

        dialog.Content = grid;
        await dialog.ShowDialog(owner);

        return confirmed;
    }
}
