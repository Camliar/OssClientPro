using System.Diagnostics;

namespace OssClientPro.Services;

/// <summary>
/// Simple file-based logger. Records all operations to app.log next to the executable.
/// Thread-safe via lock.
/// </summary>
public class LogService
{
    private readonly string _logPath;
    private readonly object _lock = new();
    private const int MaxLogSize = 5 * 1024 * 1024; // 5 MB

    public LogService()
    {
        _logPath = Path.Combine(AppContext.BaseDirectory, "app.log");
    }

    public void Info(string message)
    {
        Write("INFO", message);
    }

    public void Warn(string message)
    {
        Write("WARN", message);
    }

    public void Error(string message, Exception? ex = null)
    {
        var msg = ex != null ? $"{message} | {ex.GetType().Name}: {ex.Message}" : message;
        Write("ERROR", msg);
    }

    private void Write(string level, string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var line = $"[{timestamp}] [{level}] {message}";

        // Also output to Debug for IDE visibility
        Debug.WriteLine($"[OssClientPro] {line}");

        lock (_lock)
        {
            try
            {
                // Rotate if too large
                if (File.Exists(_logPath) && new FileInfo(_logPath).Length > MaxLogSize)
                {
                    var backup = _logPath + ".old";
                    if (File.Exists(backup)) File.Delete(backup);
                    File.Move(_logPath, backup);
                }

                File.AppendAllText(_logPath, line + Environment.NewLine);
            }
            catch
            {
                // Don't crash the app if logging fails
            }
        }
    }

    /// <summary>
    /// Replaces the log file with a fresh one and writes a startup header.
    /// </summary>
    public void InitSession()
    {
        lock (_lock)
        {
            try
            {
                File.WriteAllText(_logPath,
                    $"=== OssClientPro Session Start: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n" +
                    $"OS: {Environment.OSVersion}\n" +
                    $"Framework: .NET {Environment.Version}\n" +
                    $"Process: {Environment.ProcessId}\n\n");
            }
            catch { }
        }
    }
}
