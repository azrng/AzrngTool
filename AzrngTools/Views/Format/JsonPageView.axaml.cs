using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using AvaloniaEdit;
using AzrngTools.ViewModels.Format;

namespace AzrngTools.Views.Format
{
    public partial class JsonPageView : ViewControlBase
    {
        private readonly TextEditor? _textEditor;

        public JsonPageView()
        {
            InitializeComponent();
            _textEditor = this.FindControl<TextEditor>("JsonText");
            if (_textEditor is not null)
            {
                _textEditor.TextChanged += OnEditorTextChanged;

                // todo ：格式还有问题
                //textEditor.TextArea.TextView.ElementGenerators.Add(new TruncateLongLines());
            }
        }

        private void OnEditorTextChanged(object? sender, EventArgs e)
        {
            if (DataContext is JsonPageViewModel viewModel && _textEditor is not null)
            {
                viewModel.Original = _textEditor.Text ?? string.Empty;
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
