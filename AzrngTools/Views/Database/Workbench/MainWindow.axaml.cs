using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using AzrngTools.Controls.Database;
using AzrngTools.Models.Database;
using AzrngTools.Services.Database;
using AzrngTools.ViewModels.Database;

namespace AzrngTools.Views.Database.Workbench;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        InitializeToastService();
        RegisterNodeSelectedHandler();
        RegisterWindowChromeHandlers();
    }

    private void InitializeToastService()
    {
        if (this.FindControl<StackPanel>("ToastContainer") is { } toastContainer)
        {
            ToastService.SetContainer(toastContainer);
        }
    }

    private void RegisterNodeSelectedHandler()
    {
        if (this.FindControl<DatabaseTree>("DatabaseTree") is { } databaseTree)
        {
            databaseTree.NodeSelected += OnNodeSelected;
        }
    }

    private void RegisterWindowChromeHandlers()
    {
        if (this.FindControl<Border>("HeaderBorder") is { } headerBorder)
        {
            headerBorder.PointerPressed += OnHeaderBorderPointerPressed;
            headerBorder.DoubleTapped += OnHeaderBorderDoubleTapped;
        }

        if (this.FindControl<Button>("BtnMin") is { } btnMin)
        {
            btnMin.Click += (_, _) => WindowState = WindowState.Minimized;
        }

        if (this.FindControl<Button>("BtnMax") is { } btnMax)
        {
            btnMax.Click += (_, _) => ToggleMaximize();
        }

        if (this.FindControl<Button>("BtnClose") is { } btnClose)
        {
            btnClose.Click += (_, _) => Close();
        }
    }

    private async void OnNodeSelected(object? sender, TreeNodeItem? node)
    {
        if (node == null || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        switch (node.NodeType)
        {
            case TreeNodeType.Schema when node.Data is SchemaModel schema:
                await LoadSchemaDetailsAsync(viewModel, schema);
                break;
            case TreeNodeType.Table when node.Data is TableModel table:
                await ShowTableDetailAsync(viewModel, table);
                break;
            case TreeNodeType.View when node.Data is AzrngTools.Models.Database.ViewModel view:
                await ShowViewDetailAsync(viewModel, view);
                break;
            case TreeNodeType.StoredProcedure when node.Data is StoredProcedureModel procedure:
                await ShowProcedureDetailAsync(viewModel, procedure);
                break;
            default:
                ShowWorkspaceForNode(viewModel, node);
                break;
        }
    }

    private async Task LoadSchemaDetailsAsync(MainWindowViewModel viewModel, SchemaModel schema)
    {
        await viewModel.ActivateSchemaAsync(schema);
    }

    private static void ShowOverview(MainWindowViewModel viewModel)
    {
        viewModel.ShowOverviewPage = true;
    }

    private static void ShowWorkspaceForNode(MainWindowViewModel viewModel, TreeNodeItem node)
    {
        viewModel.ActivateWorkspaceFolder(node.Name);
    }

    private static async Task ShowTableDetailAsync(MainWindowViewModel viewModel, TableModel table)
    {
        await viewModel.ActivateTableAsync(table);
    }

    private static async Task ShowViewDetailAsync(MainWindowViewModel viewModel, AzrngTools.Models.Database.ViewModel view)
    {
        await viewModel.ActivateViewAsync(view);
    }

    private static async Task ShowProcedureDetailAsync(MainWindowViewModel viewModel, StoredProcedureModel procedure)
    {
        await viewModel.ActivateProcedureAsync(procedure);
    }


    private void OnClearBrowserSearchClick(object? sender, RoutedEventArgs e)
    {
        if (this.FindControl<DatabaseTree>("DatabaseTree") is { } databaseTree)
        {
            databaseTree.SearchText = string.Empty;
        }
    }

    private void OnHeaderBorderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsInteractiveHeaderTarget(e.Source))
        {
            return;
        }

        if (e.Pointer.Type == PointerType.Mouse)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnHeaderBorderDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (IsInteractiveHeaderTarget(e.Source))
        {
            return;
        }

        ToggleMaximize();
    }

    private static bool IsInteractiveHeaderTarget(object? source)
    {
        if (source is not Control control)
        {
            return false;
        }

        return control.GetSelfAndVisualAncestors().Any(current => current is Button
            or ComboBox
            or TextBox
            or ToggleButton
            or ListBox
            or ScrollBar);
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Normal
            ? WindowState.Maximized
            : WindowState.Normal;
    }
}
