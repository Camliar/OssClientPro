using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace OssClientPro.Views;

public partial class PreviewWindow : Window
{
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

        if (isImage)
        {
            try
            {
                PreviewImage.Source = new Bitmap(content);
                PreviewImage.IsVisible = true;
                TextScroll.IsVisible = false;
            }
            catch
            {
                PreviewText.Text = "(Unable to load image)";
                TextScroll.IsVisible = true;
            }
        }
        else
        {
            PreviewText.Text = content;
            TextScroll.IsVisible = true;
            PreviewImage.IsVisible = false;
        }
    }

    private void Close_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}
