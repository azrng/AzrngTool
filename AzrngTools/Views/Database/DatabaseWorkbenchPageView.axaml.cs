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
    private Border? _browserActionsBar;
    private Border? _browserHeaderSection;
    private Border? _browserTreeSurface;
    private Border? _workbenchToolbarStrip;
    private ScrollViewer? _browserTreeScroll;
    private ScrollViewer? _hostScrollViewer;

    public DatabaseWorkbenchPageView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        RegisterNodeSelectedHandler();
        RegisterViewportSizingTargets();
        BindHostViewportHeight();
        AssignOwnerWindow();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        AssignOwnerWindow();
    }

    private void AssignOwnerWindow()
    {
        if (DataContext is MainWindowViewModel viewModel &&
            TopLevel.GetTopLevel(this) is Window owner)
        {
            viewModel.MainWindow = owner;
        }
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
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

        UnregisterViewportSizingEvents();

        _hostScrollViewer = null;
        _browserActionsBar = null;
        _browserHeaderSection = null;
        _browserTreeScroll = null;
        _browserTreeSurface = null;
        _workbenchToolbarStrip = null;
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

    private void RegisterViewportSizingTargets()
    {
        UnregisterViewportSizingEvents();

        _browserActionsBar = this.FindControl<Border>("BrowserActionsBar");
        _browserHeaderSection = this.FindControl<Border>("BrowserHeaderSection");
        _browserTreeScroll = this.FindControl<ScrollViewer>("BrowserTreeScroll");
        _browserTreeSurface = this.FindControl<Border>("BrowserTreeSurface");
        _workbenchToolbarStrip = this.FindControl<Border>("WorkbenchToolbarStrip");

        RegisterViewportSizingEvents();
    }

    private void RegisterViewportSizingEvents()
    {
        if (_browserActionsBar != null)
        {
            _browserActionsBar.SizeChanged += OnBrowserPanelPartSizeChanged;
        }

        if (_browserHeaderSection != null)
        {
            _browserHeaderSection.SizeChanged += OnBrowserPanelPartSizeChanged;
        }

        if (_workbenchToolbarStrip != null)
        {
            _workbenchToolbarStrip.SizeChanged += OnBrowserPanelPartSizeChanged;
        }
    }

    private void UnregisterViewportSizingEvents()
    {
        if (_browserActionsBar != null)
        {
            _browserActionsBar.SizeChanged -= OnBrowserPanelPartSizeChanged;
        }

        if (_browserHeaderSection != null)
        {
            _browserHeaderSection.SizeChanged -= OnBrowserPanelPartSizeChanged;
        }

        if (_workbenchToolbarStrip != null)
        {
            _workbenchToolbarStrip.SizeChanged -= OnBrowserPanelPartSizeChanged;
        }
    }

    private void OnBrowserPanelPartSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_hostScrollViewer != null)
        {
            ApplyViewportHeight(_hostScrollViewer.Bounds.Height);
        }
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
        ApplyBrowserTreeViewportHeight(height);
    }

    private void ApplyBrowserTreeViewportHeight(double hostHeight)
    {
        if (_browserHeaderSection == null ||
            _browserActionsBar == null ||
            _browserTreeSurface == null ||
            _browserTreeScroll == null ||
            _workbenchToolbarStrip == null ||
            hostHeight <= 0)
        {
            return;
        }

        const double contentMargin = 40d;
        const double treeSurfaceVerticalMargin = 20d;
        var availablePanelHeight = hostHeight - _workbenchToolbarStrip.Bounds.Height - contentMargin;
        var treeSurfaceHeight = Math.Max(
            260d,
            availablePanelHeight -
            _browserHeaderSection.Bounds.Height -
            _browserActionsBar.Bounds.Height -
            treeSurfaceVerticalMargin);

        _browserTreeSurface.MinHeight = 0;
        _browserTreeSurface.Height = treeSurfaceHeight;
        _browserTreeSurface.MaxHeight = treeSurfaceHeight;

        var treeScrollHeight = Math.Max(240d, treeSurfaceHeight - 8d);
        _browserTreeScroll.MinHeight = 0;
        _browserTreeScroll.MaxHeight = treeScrollHeight;
    }

    private async void OnNodeSelected(object? sender, TreeNodeItem? node)
    {
        if (node == null || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        await viewModel.BrowserViewModel.EnsureNodeChildrenLoadedAsync(node);

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

    private void OnClearBrowserSearchClick(object? sender, RoutedEventArgs e)
    {
        if (_databaseTree != null)
        {
            _databaseTree.SearchText = string.Empty;
        }
    }
}

