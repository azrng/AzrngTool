using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using AzrngTools.Controls.Database;
using AzrngTools.Models.Database;
using AzrngTools.Services.Database;
using AzrngTools.ViewModels.Database;

namespace AzrngTools.Views.Database;

public partial class DatabaseWorkbenchPageView : UserControl
{
    private DatabaseTree? _databaseTree;
    private ScrollViewer? _hostScrollViewer;

    public DatabaseWorkbenchPageView()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void OnAttachedToVisualTree(object sender, VisualTreeAttachmentEventArgs e)
    {
        RegisterNodeSelectedHandler();
        BindHostViewportHeight();

        if (DataContext is MainWindowViewModel viewModel &&
            TopLevel.GetTopLevel(this) is Window owner)
        {
            viewModel.MainWindow = owner;
        }
    }

    private void OnDetachedFromVisualTree(object sender, VisualTreeAttachmentEventArgs e)
    {
        if (_databaseTree != null)
        {
            _databaseTree.NodeSelected -= OnNodeSelected;
            _databaseTree = null;
        }

        if (_hostScrollViewer != null)
        {
            _hostScrollViewer.SizeChanged -= OnHostScrollViewerSizeChanged;
        }

        _hostScrollViewer = null;
    }

    private void RegisterNodeSelectedHandler()
    {
        if (_databaseTree != null)
        {
            _databaseTree.NodeSelected -= OnNodeSelected;
        }

        _databaseTree = this.FindControl<DatabaseTree>("DatabaseTree");
        if (_databaseTree != null)
        {
            _databaseTree.NodeSelected += OnNodeSelected;
        }
    }

    private void BindHostViewportHeight()
    {
        if (_hostScrollViewer != null)
        {
            _hostScrollViewer.SizeChanged -= OnHostScrollViewerSizeChanged;
        }

        _hostScrollViewer = this.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
        if (_hostScrollViewer == null)
        {
            return;
        }

        VerticalAlignment = VerticalAlignment.Stretch;
        ApplyViewportHeight(_hostScrollViewer.Bounds.Height);
        _hostScrollViewer.SizeChanged += OnHostScrollViewerSizeChanged;
    }

    private void OnHostScrollViewerSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        ApplyViewportHeight(e.NewSize.Height);
    }

    private void ApplyViewportHeight(double height)
    {
        if (height <= 0)
        {
            return;
        }

        Height = height;
        MinHeight = 0;
        MaxHeight = height;
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
                await viewModel.ActivateSchemaAsync(schema);
                break;
            case TreeNodeType.Table when node.Data is TableModel table:
                await viewModel.ActivateTableAsync(table);
                break;
            case TreeNodeType.View when node.Data is AzrngTools.Models.Database.ViewModel view:
                await viewModel.ActivateViewAsync(view);
                break;
            case TreeNodeType.StoredProcedure when node.Data is StoredProcedureModel procedure:
                await viewModel.ActivateProcedureAsync(procedure);
                break;
            default:
                viewModel.ActivateWorkspaceFolder(node.Name);
                break;
        }
    }

    private void OnClearBrowserSearchClick(object sender, RoutedEventArgs e)
    {
        if (_databaseTree != null)
        {
            _databaseTree.SearchText = string.Empty;
        }
    }
}

#nullable enable
