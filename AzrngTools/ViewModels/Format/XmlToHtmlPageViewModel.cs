#nullable disable
using AzrngTools.Services;
using AzrngTools.Utils.Events;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;
using System.Xml.Xsl;

namespace AzrngTools.ViewModels.Format
{
    /// <summary>
    /// xml 转 html
    /// </summary>
    public partial class XmlToHtmlPageViewModel : ViewModelBase
    {
        /// <summary>输入记忆存档的页面标识（DES-SETTINGS-002 接入清单）。</summary>
        private const string PageId = "format-xml-to-html";

        private readonly IMessageService _messageService;
        private readonly IPageInputStoreService _pageInputStore;

        // 构造恢复期间的属性赋值会再次触发 OnXxxChanged，置位跳过保存提交避免恢复即回写
        private bool _isRestoring;

        public XmlToHtmlPageViewModel(IMessageService messageService, IPageInputStoreService pageInputStore)
        {
            _messageService = messageService;
            _pageInputStore = pageInputStore;
            RestoreInputs();
        }

        partial void OnXmlContentChanged(string value)
        {
            if (_isRestoring)
            {
                return;
            }

            SaveInputs();
        }

        partial void OnXsltContentChanged(string value)
        {
            if (_isRestoring)
            {
                return;
            }

            SaveInputs();
        }

        private void SaveInputs()
        {
            _pageInputStore.Save(PageId, new Dictionary<string, string?>
            {
                [nameof(XmlContent)] = PageInputValues.Encode(XmlContent),
                [nameof(XsltContent)] = PageInputValues.Encode(XsltContent),
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
                if (saved.TryGetValue(nameof(XmlContent), out var xmlContent))
                {
                    XmlContent = xmlContent;
                }

                if (saved.TryGetValue(nameof(XsltContent), out var xsltContent))
                {
                    XsltContent = xsltContent;
                }
            }
            finally
            {
                _isRestoring = false;
            }
        }

        /// <summary>
        /// xml内容
        /// </summary>
        [ObservableProperty]
        private string _xmlContent;

        /// <summary>
        /// xslt 内容
        /// </summary>
        [ObservableProperty]
        private string _xsltContent;

        /// <summary>
        /// 格式化后内容
        /// </summary>
        [ObservableProperty]
        private string _htmlContent;

        /// <summary>
        /// 转化
        /// </summary>
        [RelayCommand]
        private async Task HandleAsync()
        {
            try
            {
                if (XmlContent.IsNullOrWhiteSpace())
                {
                    _messageService.SendMessage("请输入要格式化的XML");
                    return;
                }

                if (XsltContent.IsNullOrWhiteSpace())
                {
                    _messageService.SendMessage("请输入的XSlT");
                    return;
                }

                // XslCompiledTransform 依赖运行时生成 IL，Native AOT 版本无法支持，提前给出明确提示
                if (!RuntimeFeature.IsDynamicCodeSupported)
                {
                    _messageService.SendMessage("XSLT 转换依赖运行时代码生成，AOT 发布版本暂不支持该功能。");
                    return;
                }

                var xml = XmlContent;
                var xslt = XsltContent;
                // 样式表编译加转换对大 XML 是重活，移出 UI 线程避免点击后界面冻结
                using var perf = PerfLogScope.Start("XML 转 HTML", $"输入 {PerfLogScope.DescribeSize(xml.Length)}");
                HtmlContent = await Task.Run(() => TransformToHtml(xml, xslt));
            }
            catch (Exception ex)
            {
                AppLog.LogError($"XML转HTML失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
                _messageService.SendMessage($"处理失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 编译 XSLT 样式表并执行转换
        /// </summary>
        private static string TransformToHtml(string xmlContent, string xsltContent)
        {
            var transform = new XslCompiledTransform();

            using (var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(xsltContent)))
            {
                using (var readerXsl = XmlReader.Create(memoryStream))
                {
                    transform.Load(readerXsl);
                }
            }

            var stringBuilder = new StringBuilder();

            using (var writer = XmlWriter.Create(stringBuilder,
                       new XmlWriterSettings { Indent = true, ConformanceLevel = ConformanceLevel.Auto }))
            {
                using (var memoryStream2 = new MemoryStream(Encoding.UTF8.GetBytes(xmlContent)))
                {
                    using (var readerXml = XmlReader.Create(memoryStream2))
                    {
                        transform.Transform(readerXml, writer);
                    }
                }
            }

            return stringBuilder.ToString();
        }
    }
}