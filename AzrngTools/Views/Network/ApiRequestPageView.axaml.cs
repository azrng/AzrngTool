using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace AzrngTools.Views.Network;

public partial class ApiRequestPageView : ViewControlBase
{
    private Border? _historyPane;
    private ScrollViewer? _hostScrollViewer;

    public ApiRequestPageView()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _historyPane = this.FindControl<Border>("HistoryPane");
        BindHostViewportHeight();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_hostScrollViewer != null)
        {
            _hostScrollViewer.SizeChanged -= OnHostScrollViewerSizeChanged;
        }

        _hostScrollViewer = null;
        _historyPane = null;
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

        ApplyHistoryPaneViewportHeight(_hostScrollViewer.Bounds.Height);
        _hostScrollViewer.SizeChanged += OnHostScrollViewerSizeChanged;
    }

    private void OnHostScrollViewerSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        ApplyHistoryPaneViewportHeight(e.NewSize.Height);
    }

    private void ApplyHistoryPaneViewportHeight(double hostHeight)
    {
        if (_historyPane == null || hostHeight <= 0)
        {
            return;
        }

        var availableHeight = Math.Max(320d, hostHeight - 36d);
        _historyPane.VerticalAlignment = VerticalAlignment.Top;
        _historyPane.MinHeight = 0;
        _historyPane.MaxHeight = availableHeight;
    }
}
