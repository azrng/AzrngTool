using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using AvaloniaEdit;

namespace AzrngTools.Views.Format
{
    public partial class JsonPageView : ViewControlBase
    {
        public JsonPageView()
        {
            InitializeComponent();
            var textEditor = this.FindControl<TextEditor>("JsonText");
            if (textEditor is not null)
            {
                // todo ：格式还有问题
                //textEditor.TextArea.TextView.ElementGenerators.Add(new TruncateLongLines());
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}