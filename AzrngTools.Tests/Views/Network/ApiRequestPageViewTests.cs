using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using AzrngTools.Converters.Database;
using AzrngTools.Views.Network;

namespace AzrngTools.Tests.Views.Network;

[Collection("AvaloniaHeadlessRegression")]
public class ApiRequestPageViewTests
{
    [Theory]
    [InlineData(1280)]
    [InlineData(1440)]
    [InlineData(1920)]
    public async Task Api_request_workbench_renders_the_primary_request_surface(double width)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ApiRequestTestApplication));
        await session.Dispatch(() =>
        {
            var view = new ApiRequestPageView();
            var window = new Window
            {
                Width = width,
                Height = 800,
                Content = view
            };

            window.Show();
            window.UpdateLayout();

            Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "接口调试工作台");
            Assert.Contains(view.GetVisualDescendants().OfType<TextBox>(), textBox =>
                textBox.PlaceholderText?.Contains("输入请求地址", StringComparison.Ordinal) == true);
            Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "发送请求");
            Assert.Equal(2, view.GetVisualDescendants().OfType<TabControl>().Count());

            var requestUrlInput = view.GetVisualDescendants().OfType<TextBox>().Single(textBox =>
                textBox.PlaceholderText?.Contains("输入请求地址", StringComparison.Ordinal) == true);
            Assert.True(requestUrlInput.Bounds.Width > 0, "请求地址输入框应获得可用宽度。");
            Assert.True(requestUrlInput.Bounds.Height > 0, "请求地址输入框应参与运行时布局。");

            window.Close();
        }, CancellationToken.None);
    }

    private sealed class ApiRequestTestApplication : Application
    {
        public override void Initialize()
        {
            Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://AzrngTools/"))
            {
                Source = new Uri("avares://AzrngTools/Styles/DesignTokens.axaml")
            });
            Resources["InvertedBoolConverter"] = new InvertedBoolConverter();
            Resources["EqualToZeroConverter"] = new EqualToZeroConverter();
            Resources["GreaterThanZeroConverter"] = new GreaterThanZeroConverter();
            Styles.Add(new FluentTheme());
        }
    }
}
