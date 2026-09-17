#nullable disable
using AzrngTools.Utils;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AzrngTools.ViewModels.TextHandle
{
    /// <summary>
    /// md预览
    /// </summary>
    public partial class MarkdownPageViewModel : ViewModelBase
    {
        private readonly DebouncedActionDispatcher _previewDebouncer = new(TimeSpan.FromMilliseconds(300));

        /// <summary>
        /// md文本
        /// </summary>
        [ObservableProperty]
        private string _mdText = string.Empty;

        /// <summary>
        /// 预览文本：防抖 300ms 后同步自 MdText，避免每次击键都在 UI 线程全量重排版
        /// </summary>
        [ObservableProperty]
        private string _previewText = string.Empty;

        partial void OnMdTextChanged(string value)
        {
            _previewDebouncer.Debounce(() => PreviewText = MdText);
        }
    }
}
