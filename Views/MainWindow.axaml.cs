using Avalonia.Controls;
using Avalonia.Input;
using OssClientPro.Models;
using OssClientPro.Services;
using OssClientPro.ViewModels;

namespace OssClientPro.Views;

public partial class MainWindow : Window
{
    private MenuFlyout? _rowContextFlyout;

    public MainWindow()
    {
        InitializeComponent();

        // DataGrid columns don't inherit DataContext, so we set localized headers
        // after the data context is attached
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            // Set initial column headers
            UpdateDataGridHeaders(vm.LanguageService);

            // Re-apply headers on language change
            vm.LanguageService.PropertyChanged += (_, args) =>
            {
                if (string.IsNullOrEmpty(args.PropertyName) || args.PropertyName == "Item[]")
                {
                    UpdateDataGridHeaders(vm.LanguageService);
                    // Clear cached flyout so it gets rebuilt with updated localized strings
                    _rowContextFlyout = null;
                }
            };

            // Set up right-click context menu on file rows
            FileDataGrid.CellPointerPressed += OnCellPointerPressed;
        }
    }

    private void UpdateDataGridHeaders(LanguageService lang)
    {
        if (FileDataGrid.Columns.Count >= 4)
        {
            // Column 0 = checkbox, 1 = Name, 2 = Size, 3 = LastModified
            FileDataGrid.Columns[1].Header = lang["lbl_file_name"];
            FileDataGrid.Columns[2].Header = lang["lbl_file_size"];
            FileDataGrid.Columns[3].Header = lang["lbl_last_modified"];
        }
    }

    /// <summary>
    /// Handles right-click on DataGrid cells to show the context menu.
    /// </summary>
    private void OnCellPointerPressed(object? sender, DataGridCellPointerPressedEventArgs e)
    {
        var point = e.PointerPressedEventArgs.GetCurrentPoint(this);
        if (!point.Properties.IsRightButtonPressed) return;

        if (e.Row.DataContext is not OssObjectItem item) return;
        if (DataContext is not MainViewModel vm) return;

        // Select the right-clicked file
        vm.FileList.SelectedFile = item;

        var lang = vm.LanguageService;
        var fl = vm.FileList;

        // Build context menu on demand (cached until language changes)
        if (_rowContextFlyout == null)
        {
            _rowContextFlyout = BuildContextFlyout(fl, lang);
        }

        // Update the Edit / Copy Content items' enabled state based on the clicked file
        foreach (var menuItem in _rowContextFlyout.Items.OfType<MenuItem>())
        {
            if (menuItem.Tag is string tag)
            {
                menuItem.IsEnabled = tag switch
                {
                    "Edit" or "CopyContent" => item.IsTextFile,
                    _ => true
                };
            }
        }

        _rowContextFlyout.ShowAt((Control)sender!, true);
    }

    /// <summary>
    /// Builds the right-click context flyout with all menu items.
    /// Menu items use click handlers directly to avoid DataContext binding issues
    /// inside the popup visual tree.
    /// </summary>
    private MenuFlyout BuildContextFlyout(FileListViewModel fl, LanguageService lang)
    {
        var flyout = new MenuFlyout();

        // ── View / Transfer ──
        // Preview
        var previewItem = new MenuItem { Header = lang["menu_preview"] };
        previewItem.Click += async (_, _) =>
        {
            if (fl.SelectedFile != null)
                await fl.PreviewFileCommand.ExecuteAsync(fl.SelectedFile);
        };
        flyout.Items.Add(previewItem);

        // Download
        var downloadItem = new MenuItem { Header = lang["menu_download"] };
        downloadItem.Click += async (_, _) =>
        {
            if (fl.SelectedFile != null)
                await fl.DownloadFileCommand.ExecuteAsync(fl.SelectedFile);
        };
        flyout.Items.Add(downloadItem);

        flyout.Items.Add(new Separator());

        // ── Data operations ──
        // Edit (text files only)
        var editItem = new MenuItem { Header = lang["menu_edit"], Tag = "Edit" };
        editItem.Click += async (_, _) =>
        {
            if (fl.SelectedFile != null)
                await fl.EditFileCommand.ExecuteAsync(fl.SelectedFile);
        };
        flyout.Items.Add(editItem);

        // Delete
        var deleteItem = new MenuItem { Header = lang["menu_delete"] };
        deleteItem.Click += async (_, _) =>
        {
            if (fl.SelectedFile != null)
                await fl.DeleteFileCommand.ExecuteAsync(fl.SelectedFile);
        };
        flyout.Items.Add(deleteItem);

        flyout.Items.Add(new Separator());

        // ── Copy operations ──
        // Copy Content (text files only)
        var copyContentItem = new MenuItem { Header = lang["menu_copy_content"], Tag = "CopyContent" };
        copyContentItem.Click += async (_, _) =>
        {
            if (fl.SelectedFile != null)
                await fl.CopyContentCommand.ExecuteAsync(fl.SelectedFile);
        };
        flyout.Items.Add(copyContentItem);

        // Copy File Name
        var copyNameItem = new MenuItem { Header = lang["menu_copy_file_name"] };
        copyNameItem.Click += async (_, _) =>
        {
            if (fl.SelectedFile != null)
                await fl.CopyFileNameCommand.ExecuteAsync(fl.SelectedFile);
        };
        flyout.Items.Add(copyNameItem);

        // Copy File Link
        var copyLinkItem = new MenuItem { Header = lang["menu_copy_file_link"] };
        copyLinkItem.Click += async (_, _) =>
        {
            if (fl.SelectedFile != null)
                await fl.CopyFileLinkCommand.ExecuteAsync(fl.SelectedFile);
        };
        flyout.Items.Add(copyLinkItem);

        // Copy File Info
        var copyInfoItem = new MenuItem { Header = lang["menu_copy_file_info"] };
        copyInfoItem.Click += async (_, _) =>
        {
            if (fl.SelectedFile != null)
                await fl.CopyFileInfoCommand.ExecuteAsync(fl.SelectedFile);
        };
        flyout.Items.Add(copyInfoItem);

        return flyout;
    }
}
