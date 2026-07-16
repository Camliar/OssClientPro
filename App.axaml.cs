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
                Debug.WriteLine($"[OssClientPro] FATAL startup error: {ex}");
                // In debug mode, re-throw so the debugger catches it
                throw;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void InitializeApp(IClassicDesktopStyleApplicationLifetime desktop)
    {
        Debug.WriteLine("[OssClientPro] === InitializeApp START ===");

        var languagesPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Languages");
        Debug.WriteLine($"[OssClientPro] 1. Languages path: {languagesPath}");

        var configService = new ConfigService();
        var ossService = new OssService();
        Debug.WriteLine("[OssClientPro] 2. Services created");

        // Load saved config synchronously to avoid any async complexity during startup
        var config = LoadConfigSync(configService);
        var initialLanguage = !string.IsNullOrEmpty(config.Language)
            ? config.Language
            : LanguageService.DetectSystemLanguage();
        Debug.WriteLine($"[OssClientPro] 3. Initial language: {initialLanguage}");

        Debug.WriteLine("[OssClientPro] 4. Creating LanguageService...");
        _languageService = new LanguageService(languagesPath);
        Debug.WriteLine("[OssClientPro] 5. LanguageService created, setting language...");
        _languageService.SetLanguage(initialLanguage);
        Debug.WriteLine("[OssClientPro] 6. Language set, adding to Resources...");
        Resources["Localization"] = _languageService;
        Debug.WriteLine("[OssClientPro] 7. Localization registered");

        Debug.WriteLine("[OssClientPro] 8. Creating MainViewModel...");
        var mainViewModel = new MainViewModel(configService, ossService, _languageService);
        Debug.WriteLine("[OssClientPro] 9. MainViewModel created");

        // Populate settings fields from loaded config
        Debug.WriteLine("[OssClientPro] 10. Loading settings config...");
        mainViewModel.Settings.LoadConfig(config);
        Debug.WriteLine("[OssClientPro] 11. Settings config loaded");

        Debug.WriteLine("[OssClientPro] 12. Creating MainWindow...");
        var mainWindow = new MainWindow
        {
            DataContext = mainViewModel
        };
        Debug.WriteLine("[OssClientPro] 13. MainWindow created");

        mainViewModel.FileList.ConfirmHandler = async (title, message) =>
        {
            var result = false;
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                result = await ShowConfirmDialog(mainWindow, title, message);
            });
            return result;
        };
        Debug.WriteLine("[OssClientPro] 14. ConfirmHandler wired");

        mainViewModel.FileList.MainWindow = mainWindow;
        desktop.MainWindow = mainWindow;
        Debug.WriteLine("[OssClientPro] 15. MainWindow set. === INIT COMPLETE ===");
    }

    /// <summary>
    /// Synchronously loads config from disk. Uses Task.Run internally so
    /// GetAwaiter().GetResult() is safe (no SynchronizationContext yet at startup).
    /// </summary>
    private static OssConfig LoadConfigSync(ConfigService configService)
    {
        return configService.LoadConfigAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Shows a simple yes/no confirmation dialog and returns the user's choice.
    /// </summary>
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

        var panel = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16
        };

        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap
        });

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
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
        panel.Children.Add(buttonPanel);

        dialog.Content = panel;
        await dialog.ShowDialog(owner);

        return confirmed;
    }
}
