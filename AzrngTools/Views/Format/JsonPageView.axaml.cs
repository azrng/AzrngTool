using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;

namespace AzrngTools.Views.Format
{
    public partial class JsonPageView : ViewControlBase
    {
        private Border? _headerCard;
        private Grid? _rootGrid;
        private Border? _workspaceCard;
        private ScrollViewer? _hostScrollViewer;

        public JsonPageView()
        {
            InitializeComponent();
            _headerCard = this.FindControl<Border>("JsonHeaderCard");
            _rootGrid = this.FindControl<Grid>("RootGrid");
            _workspaceCard = this.FindControl<Border>("JsonWorkspaceCard");

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

            _hostScrollViewer = null;

            if (_headerCard != null)
            {
                _headerCard.SizeChanged -= OnHeaderCardSizeChanged;
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
            if (_rootGrid == null || _headerCard == null || _workspaceCard == null || hostHeight <= 0)
            {
                return;
            }

            const double outerVerticalMargin = 30d;
            const double pageRowSpacing = 12d;
            var rootHeight = Math.Max(360d, hostHeight - outerVerticalMargin);
            var headerHeight = _headerCard.Bounds.Height;
            var workspaceHeight = Math.Max(260d, rootHeight - headerHeight - pageRowSpacing);

            _rootGrid.Height = rootHeight;
            _workspaceCard.MinHeight = 0;
            _workspaceCard.Height = workspaceHeight;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
