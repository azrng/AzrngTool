using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AzrngTools.Views.Encode
{
    public partial class Base64EncodePageView : ViewControlBase
    {
        private Border? _headerCard;
        private Grid? _rootGrid;
        private Grid? _workspaceGrid;
        private ScrollViewer? _hostScrollViewer;

        public Base64EncodePageView()
        {
            InitializeComponent();
            _headerCard = this.FindControl<Border>("HeaderCard");
            _rootGrid = this.FindControl<Grid>("RootGrid");
            _workspaceGrid = this.FindControl<Grid>("WorkspaceGrid");

            AttachedToVisualTree += OnAttachedToVisualTree;
            DetachedFromVisualTree += OnDetachedFromVisualTree;
        }

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            BindHostViewportHeight();

            if (_headerCard != null)
            {
                _headerCard.SizeChanged += OnHeaderCardSizeChanged;
            }
        }

        private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (_hostScrollViewer != null)
            {
                _hostScrollViewer.SizeChanged -= OnHostScrollViewerSizeChanged;
            }

            if (_headerCard != null)
            {
                _headerCard.SizeChanged -= OnHeaderCardSizeChanged;
            }

            _hostScrollViewer = null;
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

            ApplyViewportHeight(_hostScrollViewer.Bounds.Height);
            _hostScrollViewer.SizeChanged += OnHostScrollViewerSizeChanged;
        }

        private void OnHostScrollViewerSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            ApplyViewportHeight(e.NewSize.Height);
        }

        private void OnHeaderCardSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            if (_hostScrollViewer != null)
            {
                ApplyViewportHeight(_hostScrollViewer.Bounds.Height);
            }
        }

        private void ApplyViewportHeight(double hostHeight)
        {
            if (_rootGrid == null || _headerCard == null || _workspaceGrid == null || hostHeight <= 0)
            {
                return;
            }

            const double outerVerticalMargin = 30d;
            const double pageRowSpacing = 12d;
            var rootHeight = Math.Max(520d, hostHeight - outerVerticalMargin);
            var workspaceHeight = Math.Max(360d, rootHeight - _headerCard.Bounds.Height - pageRowSpacing);

            _rootGrid.Height = rootHeight;
            _workspaceGrid.Height = workspaceHeight;
        }
    }
}
