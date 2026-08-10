using Avalonia.Controls;

namespace OssClientPro.Views;

public partial class EditWindow : Window
{
    /// <summary>
    /// Whether the user clicked Save (true) or Cancel/closed (false).
    /// </summary>
    public bool Saved { get; private set; }

    /// <summary>
    /// The edited text content after Save.
    /// </summary>
    public string EditedText => EditTextBox.Text ?? string.Empty;

    public EditWindow() : this(string.Empty, "Edit") { }

    /// <summary>
    /// Creates an editable text window pre-loaded with the given content.
    /// </summary>
    /// <param name="content">Initial text content to edit.</param>
    /// <param name="title">Window title (typically the file name).</param>
    public EditWindow(string content, string title)
    {
        InitializeComponent();
        Title = $"{title} — Edit";
        EditTextBox.Text = content;
    }

    private void Save_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Saved = true;
        Close();
    }

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Saved = false;
        Close();
    }
}
