using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;

namespace OssClientPro.Views;

public partial class PreviewWindow : Window
{
    private readonly string _textContent = string.Empty;
    private readonly bool _isImage;

    public PreviewWindow() : this(string.Empty, "Preview", false) { }

    /// <summary>
    /// Creates a preview window for text content or a local image file.
    /// </summary>
    /// <param name="content">Text content or file path (for images).</param>
    /// <param name="title">Window title.</param>
    /// <param name="isImage">If true, <paramref name="content"/> is a file path to an image.</param>
    public PreviewWindow(string content, string title, bool isImage)
    {
        InitializeComponent();
        Title = $"{title} — Preview";
        _isImage = isImage;

        if (isImage)
        {
            try
            {
                PreviewImage.Source = new Bitmap(content);
                PreviewImage.IsVisible = true;
                TextScroll.IsVisible = false;
                CopyButton.IsEnabled = false;
            }
            catch
            {
                PreviewText.Text = "(Unable to load image)";
                TextScroll.IsVisible = true;
            }
        }
        else
        {
            _textContent = content;
            PreviewText.Text = content;
            TextScroll.IsVisible = true;
            PreviewImage.IsVisible = false;
        }
    }

    private async void Copy_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isImage || string.IsNullOrEmpty(_textContent)) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard != null)
            await topLevel.Clipboard.SetTextAsync(_textContent);
    }

    private void Close_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}
