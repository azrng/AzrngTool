using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using AzrngTools.Views.Pdf;

namespace AzrngTools.Tests.Views.Pdf;

/// <summary>
/// PDF 管理页 headless 冒烟：导出 Markdown 功能卡渲染与既有功能卡完整性（DES-PDF-002 界面原型）。
/// </summary>
[Collection("AvaloniaHeadlessRegression")]
public class PdfManagementPageViewTests
{
    [Fact]
    public async Task Pdf_management_page_renders_markdown_export_card_with_existing_cards()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(PdfManagementTestApplication));
        await session.Dispatch(() =>
        {
            var view = new PdfManagementPageView();
            var window = new Window
            {
                Width = 1280,
                Height = 860,
                Content = view
            };

            window.Show();
            window.UpdateLayout();

            var texts = view.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text).ToList();

            // 新增「导出 Markdown」功能卡关键元素
            Assert.Contains("导出 Markdown", texts);
            Assert.Contains("导出图片到 .assets 目录", texts);
            Assert.Contains("开始转换", texts);
            var markdownOutput = view.GetVisualDescendants().OfType<TextBox>().Single(textBox =>
                textBox.PlaceholderText?.Contains("Markdown 输出文件", StringComparison.Ordinal) == true);
            Assert.True(markdownOutput.Bounds.Width > 0, "Markdown 输出框应获得可用宽度。");

            // 既有功能卡不回归
            Assert.Contains("分割 PDF", texts);
            Assert.Contains("导出 Word", texts);
            Assert.Contains("开始分割", texts);
            Assert.Contains("导出 Word", texts);
            Assert.Contains("快捷操作", texts);
            Assert.Contains(view.GetVisualDescendants().OfType<CheckBox>(), checkBox =>
                checkBox.Content?.ToString()?.Contains("逐页导出为多个 PDF") == true);

            window.Close();
        }, CancellationToken.None);
    }

    private sealed class PdfManagementTestApplication : Application
    {
        public override void Initialize()
        {
            Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://AzrngTools/"))
            {
                Source = new Uri("avares://AzrngTools/Styles/DesignTokens.axaml")
            });
            Styles.Add(new FluentTheme());
        }
    }
}
