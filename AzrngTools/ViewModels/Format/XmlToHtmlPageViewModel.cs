#nullable disable
using AzrngTools.Utils.Events;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
        private readonly IMessageService _messageService;

        public XmlToHtmlPageViewModel(IMessageService messageService)
        {
            _messageService = messageService;
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
        private void Handle()
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

                var transform = new XslCompiledTransform();

                using (var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(XsltContent)))
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
                    using (var memoryStream2 = new MemoryStream(Encoding.UTF8.GetBytes(XmlContent)))
                    {
                        using (var readerXml = XmlReader.Create(memoryStream2))
                        {
                            transform.Transform(readerXml, writer);
                        }
                    }
                }

                HtmlContent = stringBuilder.ToString();
            }
            catch (Exception ex)
            {
                _messageService.SendMessage($"处理失败：{ex.Message}");
            }
        }
    }
}