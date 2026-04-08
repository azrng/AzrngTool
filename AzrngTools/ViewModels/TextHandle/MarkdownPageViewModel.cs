#nullable disable
using CommunityToolkit.Mvvm.ComponentModel;

namespace AzrngTools.ViewModels.TextHandle
{
    /// <summary>
    /// md预览
    /// </summary>
    public partial class MarkdownPageViewModel : ViewModelBase
    {
        /// <summary>
        /// md文本
        /// </summary>
        [ObservableProperty]
        private string _mdText;
    }
}