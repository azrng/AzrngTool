using Avalonia.Markup.Xaml;

namespace AzrngTools.Views.Encode
{
    public partial class JwtEncodePageView : ViewControlBase
    {
        public JwtEncodePageView()
        {
            InitializeComponent();
            
            // var textEditor = this.FindControl<TextEditor>("JwtText");
            // if (textEditor is not null)
            //     //textEditor.TextArea.TextView.ElementGenerators.Add(new TruncateLongLines());
            // }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}