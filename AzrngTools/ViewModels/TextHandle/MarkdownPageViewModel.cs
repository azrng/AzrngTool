#nullable disable
using AzrngTools.Services;
using AzrngTools.Utils;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AzrngTools.ViewModels.TextHandle
{
    /// <summary>
    /// md预览
    /// </summary>
    public partial class MarkdownPageViewModel : ViewModelBase
    {
        /// <summary>输入记忆存档的页面标识（DES-SETTINGS-002 接入清单）。</summary>
        private const string PageId = "texthandle-markdown";

        private readonly DebouncedActionDispatcher _previewDebouncer = new(TimeSpan.FromMilliseconds(300));
        private readonly IPageInputStoreService _pageInputStore;

        // 构造恢复期间的属性赋值会再次触发 OnMdTextChanged，置位跳过保存提交避免恢复即回写
        private bool _isRestoring;

        public MarkdownPageViewModel(IPageInputStoreService pageInputStore)
        {
            _pageInputStore = pageInputStore;
            RestoreInputs();
        }

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
            if (!_isRestoring)
            {
                SaveInputs();
            }

            _previewDebouncer.Debounce(() => PreviewText = MdText);
        }

        private void SaveInputs()
        {
            _pageInputStore.Save(PageId, new Dictionary<string, string?>
            {
                [nameof(MdText)] = PageInputValues.Encode(MdText),
            });
        }

        private void RestoreInputs()
        {
            var saved = _pageInputStore.TryLoad(PageId);
            if (saved.Count == 0)
            {
                return;
            }

            _isRestoring = true;
            try
            {
                if (saved.TryGetValue(nameof(MdText), out var mdText))
                {
                    MdText = mdText;
                }
            }
            finally
            {
                _isRestoring = false;
            }
        }
    }
}
