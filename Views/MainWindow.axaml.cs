using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using OssClientPro.Models;
using OssClientPro.Services;
using OssClientPro.ViewModels;

namespace OssClientPro.Views;

public partial class MainWindow : Window
{
    /// <summary>Context menu for file rows — shared by the grid and tree file nodes.</summary>
    private MenuFlyout? _rowContextFlyout;

    /// <summary>Context menu for directory nodes in the tree view.</summary>
    private MenuFlyout? _folderContextFlyout;

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
                    // Clear cached flyouts so they get rebuilt with updated localized strings
                    _rowContextFlyout = null;
                    _folderContextFlyout = null;
                }
            };

            // Set up right-click context menu on file rows
            FileDataGrid.CellPointerPressed += OnCellPointerPressed;

            // TreeViewItem handles pointer presses for its own selection, so listen on
            // the bubble route with handledEventsToo to still receive them.
            FileTreeView.AddHandler(InputElement.PointerPressedEvent, OnTreePointerPressed,
                RoutingStrategies.Bubble, handledEventsToo: true);
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

        ShowRowFlyout((Control)sender!, item, vm);
    }

    /// <summary>
    /// Handles right-click inside the tree view. Directory and file nodes share the
    /// same flyout plumbing as the grid.
    /// </summary>
    private void OnTreePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsRightButtonPressed) return;

        if (FindNode(e.Source as Visual) is not { } node) return;
        if (DataContext is not MainViewModel vm) return;

        ShowRowFlyout(FileTreeView, node, vm);
    }

    /// <summary>
    /// Walks up the visual tree from the clicked element to the item it belongs to.
    /// </summary>
    /// <param name="source">Innermost clicked element — a glyph, label or checkbox.</param>
    private static OssObjectItem? FindNode(Visual? source)
    {
        for (var current = source; current != null; current = current.GetVisualParent())
        {
            if (current is StyledElement { DataContext: OssObjectItem item })
                return item;
        }
        return null;
    }

    /// <summary>
    /// Selects <paramref name="item"/> and shows its context menu. Directory nodes get
    /// a short folder-only menu; file nodes get the full menu, with the text-file-only
    /// entries enabled per item.
    /// </summary>
    private void ShowRowFlyout(Control target, OssObjectItem item, MainViewModel vm)
    {
        var lang = vm.LanguageService;
        var fl = vm.FileList;

        // Select the right-clicked item so commands and UploadPrefix act on it.
        // Selecting a directory clears SelectedFile (see OnSelectedNodeChanged).
        fl.SelectedNode = item;

        if (item.IsFolder)
        {
            _folderContextFlyout ??= BuildFolderContextFlyout(fl, lang);
            _folderContextFlyout.ShowAt(target, true);
            return;
        }

        // Build context menu on demand (cached until language changes)
        _rowContextFlyout ??= BuildContextFlyout(fl, lang);

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

        _rowContextFlyout.ShowAt(target, true);
    }

    /// <summary>
    /// Builds the context menu for a directory node. Only directory-level actions
    /// apply — there is no OSS object behind the node to preview, download or delete.
    /// </summary>
    private MenuFlyout BuildFolderContextFlyout(FileListViewModel fl, LanguageService lang)
    {
        var flyout = new MenuFlyout();

        var uploadItem = new MenuItem { Header = lang["menu_upload_here"] };
        uploadItem.Click += async (_, _) =>
        {
            if (fl.SelectedNode != null)
                await fl.UploadHereCommand.ExecuteAsync(fl.SelectedNode);
        };
        flyout.Items.Add(uploadItem);

        var copyPathItem = new MenuItem { Header = lang["menu_copy_dir_name"] };
        copyPathItem.Click += async (_, _) =>
        {
            if (fl.SelectedNode != null)
                await fl.CopyDirectoryNameCommand.ExecuteAsync(fl.SelectedNode);
        };
        flyout.Items.Add(copyPathItem);

        return flyout;
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
