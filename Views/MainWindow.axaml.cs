using Avalonia.Controls;
using OssClientPro.Services;
using OssClientPro.ViewModels;

namespace OssClientPro.Views;

public partial class MainWindow : Window
{
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
                }
            };
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
}
