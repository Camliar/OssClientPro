using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Xml.Linq;

namespace OssClientPro.Services;

/// <summary>
/// Manages multi-language localization using .resx resource files.
/// Provides an indexer for XAML bindings and supports runtime language switching.
/// </summary>
public class LanguageService : INotifyPropertyChanged
{
    private readonly Dictionary<string, string> _strings = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _languagesBasePath;

    /// <summary>
    /// Supported cultures for the application.
    /// </summary>
    public static readonly ReadOnlyCollection<CultureInfo> SupportedCultures = new(
    [
        new CultureInfo("zh-CN"),
        new CultureInfo("en-US")
    ]);

    /// <summary>
    /// Default fallback culture.
    /// </summary>
    public const string DefaultCulture = "en-US";

    /// <summary>
    /// Currently active culture.
    /// </summary>
    public CultureInfo CurrentCulture { get; private set; }

    /// <summary>
    /// Time display format: "auto" (follow OS), "12h", or "24h".
    /// Set from OssConfig.TimeFormat when loading settings.
    /// </summary>
    public string TimeFormat { get; set; } = "auto";

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Initializes a new instance of <see cref="LanguageService"/> and loads the initial language.
    /// </summary>
    /// <param name="languagesBasePath">Absolute path to the Assets/Languages directory.</param>
    public LanguageService(string languagesBasePath)
    {
        _languagesBasePath = languagesBasePath;
        CurrentCulture = CultureInfo.CurrentCulture;
        LoadLanguage(CurrentCulture.Name);
    }

    /// <summary>
    /// Indexer for XAML bindings. Returns the localized string for the given key.
    /// Usage in .axaml: {Binding [btn_upload], Source={StaticResource Localization}}
    /// </summary>
    public string this[string key]
    {
        get
        {
            if (_strings.TryGetValue(key, out var value))
                return value;

            // Return the key itself as fallback so missing keys are visible in the UI
            return $"#{key}";
        }
    }

    /// <summary>
    /// Loads the .resx file for the specified culture name.
    /// Falls back to the default culture and then Strings.resx if a specific file is not found.
    /// </summary>
    public void LoadLanguage(string cultureName)
    {
        _strings.Clear();

        // Priority: specific culture → default culture → base Strings.resx
        var loaded = false;

        if (!string.IsNullOrEmpty(cultureName))
        {
            loaded = TryLoadResx(cultureName);
        }

        // Fall back to Strings.resx (no culture suffix) if specific file not found
        if (!loaded)
        {
            var basePath = Path.Combine(_languagesBasePath, "Strings.resx");
            if (File.Exists(basePath))
            {
                LoadResxFile(basePath);
            }
        }

        try
        {
            CurrentCulture = new CultureInfo(cultureName);
        }
        catch (CultureNotFoundException)
        {
            CurrentCulture = new CultureInfo(DefaultCulture);
        }

        // Notify all bindings that the indexer has changed.
        // "Item[]" is the standard property name for indexed properties in WPF/Avalonia.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }

    /// <summary>
    /// Switches the current language at runtime and notifies all bound UIs.
    /// </summary>
    /// <param name="cultureName">Culture name, e.g. "zh-CN" or "en-US".</param>
    public void SetLanguage(string cultureName)
    {
        LoadLanguage(cultureName);
    }

    /// <summary>
    /// Formats a file size in bytes to a human-readable string (KB/MB/GB).
    /// </summary>
    public string FormatFileSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} {this["size_bytes"]}",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} {this["size_kb"]}",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):F2} {this["size_mb"]}",
            _ => $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} {this["size_gb"]}"
        };
    }

    /// <summary>
    /// Formats a DateTime according to the current culture and time format setting.
    /// </summary>
    public string FormatDateTime(DateTime dateTime)
    {
        var local = dateTime.ToLocalTime();
        var use24h = TimeFormat switch
        {
            "24h" => true,
            "12h" => false,
            _ => IsSystem24Hour() // "auto" — detect from OS
        };

        var datePart = local.ToString("d", CurrentCulture);
        var timePart = use24h
            ? local.ToString("HH:mm")
            : local.ToString("hh:mm tt", CurrentCulture);
        return $"{datePart} {timePart}";
    }

    /// <summary>
    /// Detects whether the operating system uses 24-hour time format.
    /// </summary>
    private static bool IsSystem24Hour()
    {
        try
        {
            var pattern = CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern;
            // If the pattern contains uppercase H (24-hour), the system uses 24h
            return pattern.Contains('H');
        }
        catch
        {
            return true; // default to 24h on error
        }
    }

    /// <summary>
    /// Detects the OS/system language and maps it to the closest supported culture.
    /// </summary>
    public static string DetectSystemLanguage()
    {
        var current = CultureInfo.CurrentCulture.Name;

        // Check for exact match
        foreach (var culture in SupportedCultures)
        {
            if (string.Equals(current, culture.Name, StringComparison.OrdinalIgnoreCase))
                return culture.Name;
        }

        // Check for parent match (e.g. "zh" → "zh-CN")
        foreach (var culture in SupportedCultures)
        {
            if (current.StartsWith(culture.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase))
                return culture.Name;
        }

        return DefaultCulture;
    }

    /// <summary>
    /// Attempts to load a .resx file for the specified culture name.
    /// Returns true if a file was found and loaded.
    /// </summary>
    private bool TryLoadResx(string cultureName)
    {
        // Try exact match first: Strings.zh-CN.resx
        var path = Path.Combine(_languagesBasePath, $"Strings.{cultureName}.resx");
        if (File.Exists(path))
        {
            LoadResxFile(path);
            return true;
        }

        // Try parent culture: if zh-CN not found, try zh.resx
        try
        {
            var culture = new CultureInfo(cultureName);
            if (!string.IsNullOrEmpty(culture.Parent?.Name) &&
                culture.Parent.Name != culture.Name)
            {
                path = Path.Combine(_languagesBasePath, $"Strings.{culture.Parent.Name}.resx");
                if (File.Exists(path))
                {
                    LoadResxFile(path);
                    return true;
                }
            }
        }
        catch (CultureNotFoundException)
        {
            // Ignore; will fall through to default
        }

        return false;
    }

    /// <summary>
    /// Lightweight .resx XML parser that reads <data> elements.
    /// Avoids dependency on System.Windows.Forms for cross-platform compatibility.
    /// </summary>
    private void LoadResxFile(string filePath)
    {
        try
        {
            var doc = XDocument.Load(filePath);
            var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;

            foreach (var dataElement in doc.Descendants("data"))
            {
                var name = dataElement.Attribute("name")?.Value;
                var valueElement = dataElement.Element("value");
                if (name != null && valueElement != null)
                {
                    _strings[name] = valueElement.Value;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LanguageService] Failed to load {filePath}: {ex.Message}");
        }
    }
}
