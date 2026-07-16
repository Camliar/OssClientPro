using Avalonia;
using System.Diagnostics;

namespace OssClientPro;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            App.Log.Error("FATAL crash in Main", ex);
            var crashLog = Path.Combine(AppContext.BaseDirectory, "crash.log");
            File.WriteAllText(crashLog,
                $"=== OssClientPro Crash Report ===\n" +
                $"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                $"OS:   {Environment.OSVersion}\n" +
                $"Type: {ex.GetType().FullName}\n" +
                $"Message: {ex.Message}\n" +
                $"Stack:\n{ex}\n");
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
