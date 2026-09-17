using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AzrngTools.Tests.Views;

// 测试用 App：继承真实 App 复用资源图与 DI 装配，跳过启动更新检查
// （测试进程入口程序集是 testhost，RegisterBusinessServices 扫描不到 IAppUpdateCoordinatorService）
internal sealed class CaptureApp : AzrngTools.App
{
    public override void OnFrameworkInitializationCompleted()
    {
        Program.RegisterUiThreadExceptionHandler();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = Services.GetRequiredService<global::AzrngTools.Views.MainWindow>();
        }
    }
}

// 头部还原回归：旧版两行标题 + 标语的结构与高度断言（T029 UrsaWindow 标题栏 52px 改造）
public class MainWindowHeaderRestoreTests
{
    [Fact]
    public async Task Header_restored_layout_matches_legacy_design()
    {
        await using var session = Avalonia.Headless.HeadlessUnitTestSession.StartNew(typeof(CaptureApp));
        await session.Dispatch(() =>
        {
            var app = Assert.IsType<CaptureApp>(Application.Current);
            var window = Assert.IsType<global::AzrngTools.Views.MainWindow>(
                app.Services.GetRequiredService<global::AzrngTools.Views.MainWindow>());
            window.Show();

            // 1) Ursa 窗口装饰标题栏高度已被样式覆盖为 52（模板加载晚于挂载时可能尚未创建，允许缺省）
            var decorations = window.GetLogicalDescendants()
                .OfType<Avalonia.Controls.Chrome.WindowDrawnDecorations>()
                .FirstOrDefault()
                ?? window.GetVisualDescendants()
                    .OfType<Avalonia.Controls.Chrome.WindowDrawnDecorations>()
                    .FirstOrDefault();
            if (decorations is not null)
            {
                Assert.Equal(52, decorations.TitleBarHeight);
            }

            // 2) 内容区标题底条（内容网格第 0 行 Border）同为 52 高
            var rootGrid = Assert.IsType<Grid>(window.Content);
            var strip = Assert.IsType<Border>(rootGrid.Children[0]);
            Assert.Equal(52, strip.Bounds.Height);

            // 3) 悬浮标题栏：两行标题（标题在上、副标题在下）居左
            var titleBar = window.GetVisualDescendants().OfType<Ursa.Controls.TitleBar>().Single();
            var titles = titleBar.GetVisualDescendants().OfType<TextBlock>()
                .Where(t => t.Classes.Contains("app-title") || t.Classes.Contains("app-subtitle")).ToList();
            Assert.Equal(2, titles.Count);
            var title = titles.Single(t => t.Classes.Contains("app-title"));
            var subtitle = titles.Single(t => t.Classes.Contains("app-subtitle"));
            Assert.Equal("AzrngTools", title.Text);
            Assert.Equal("开发工具集 · 支持分组浏览与搜索", subtitle.Text);
            Assert.True(subtitle.Bounds.Bottom > title.Bounds.Bottom, "副标题应在标题下方");
            Assert.True(title.Bounds.Left < window.Width / 2, "标题区应居左");

            // 4) 右侧：标语在主题开关左侧，且整组位于标题栏右侧
            var motto = titleBar.GetVisualDescendants().OfType<TextBlock>()
                .Single(t => t.Classes.Contains("titlebar-motto"));
            Assert.Equal("放下个人素质，享受缺德人生", motto.Text);
            var toggle = titleBar.GetVisualDescendants().OfType<Ursa.Controls.ThemeToggleButton>().Single();
            Assert.True(motto.Bounds.Right <= toggle.Bounds.Left, "标语应在主题开关左侧");
            // Bounds 为父容器相对坐标，换算到窗口坐标系验证整组位于右半侧
            var mottoInWindow = motto.TranslatePoint(new Point(0, 0), window)!.Value;
            Assert.True(mottoInWindow.X > window.Width / 2, $"标语组应位于窗口右侧，实际 X={mottoInWindow.X}");

            // 5) 装饰主题覆写生效：应用资源中的 KEY_URSAWINDOW_DRAWN_DECORATIONS 是我们的覆写版
            //    （DefaultTitleBarHeight=52；PART_OverlayPanel 模板带 8px 右边距，关闭按钮不贴右缘）
            Assert.True(app.Resources.TryGetResource(
                    Ursa.Controls.UrsaWindow.KEY_URSAWINDOW_DRAWN_DECORATIONS, null, out var themeResource),
                "应用资源应能解析到覆写的窗口装饰主题");
            var decorationsTheme = Assert.IsType<Avalonia.Styling.ControlTheme>(themeResource);
            var heightSetter = decorationsTheme.Setters
                .Select(s => Assert.IsType<Avalonia.Styling.Setter>(s))
                .Single(s => s.Property == Avalonia.Controls.Chrome.WindowDrawnDecorations.DefaultTitleBarHeightProperty);
            Assert.Equal(52d, heightSetter.Value);

            // 6) 关闭按钮不贴窗口右缘（装饰层仅在实机渲染，Headless 可视树无此层则跳过）
            var closeButton = window.GetVisualDescendants().OfType<Button>()
                .FirstOrDefault(b => b.Name == "PART_CloseButton");
            if (closeButton is not null)
            {
                var closeRight = closeButton.TranslatePoint(new Point(closeButton.Bounds.Width, 0), window)!.Value.X;
                var rightGap = window.Width - closeRight;
                Assert.True(rightGap >= 8, $"关闭按钮距窗口右缘应不小于 8px，实际 {rightGap:0.#}px");
            }
        }, CancellationToken.None);
    }
}
