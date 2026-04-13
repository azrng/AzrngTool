using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AzrngTools.ViewModels.Format;

namespace AzrngTools.Views.Format
{
    public partial class JsonPageView : ViewControlBase
    {
        private Border? _workspaceCard;
        private ScrollViewer? _hostScrollViewer;
        private readonly TextEditor? _textEditor;

        public JsonPageView()
        {
            InitializeComponent();
            _workspaceCard = this.FindControl<Border>("JsonWorkspaceCard");
            _textEditor = this.FindControl<TextEditor>("JsonText");
            if (_textEditor is not null)
            {
                _textEditor.TextChanged += OnEditorTextChanged;

                // todo ：格式还有问题
                //textEditor.TextArea.TextView.ElementGenerators.Add(new TruncateLongLines());
            }

            AttachedToVisualTree += OnAttachedToVisualTree;
            DetachedFromVisualTree += OnDetachedFromVisualTree;
        }

        private void OnEditorTextChanged(object? sender, EventArgs e)
        {
            if (DataContext is JsonPageViewModel viewModel && _textEditor is not null)
            {
                viewModel.Original = _textEditor.Text ?? string.Empty;
            }
        }

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            BindHostViewportHeight();
        }

        private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (_hostScrollViewer != null)
            {
                _hostScrollViewer.SizeChanged -= OnHostScrollViewerSizeChanged;
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

        private void ApplyViewportHeight(double hostHeight)
        {
            if (_workspaceCard == null || _textEditor == null || hostHeight <= 0)
            {
                return;
            }

            var workspaceHeight = Math.Max(320d, hostHeight - 64d);
            var editorHeight = Math.Max(240d, workspaceHeight - 78d);

            _workspaceCard.VerticalAlignment = VerticalAlignment.Top;
            _workspaceCard.MinHeight = 0;
            _workspaceCard.MaxHeight = workspaceHeight;
            _textEditor.MinHeight = 0;
            _textEditor.Height = editorHeight;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
