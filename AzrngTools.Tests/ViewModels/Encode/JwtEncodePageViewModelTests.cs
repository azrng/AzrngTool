using AzrngTools.Tests.TestDoubles;
using AzrngTools.Behaviors;
using AzrngTools.ViewModels.Encode;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AzrngTools.Tests.ViewModels.Encode;

[Collection("AvaloniaHeadlessRegression")]
public class JwtEncodePageViewModelTests
{
    [Fact]
    public async Task SettingValidJwt_ShouldDecodeHeaderAndPayload()
    {
        var messageService = new TestMessageService();
        var viewModel = new JwtEncodePageViewModel(messageService, TimeSpan.Zero)
        {
            Original = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c"
        };

        // 解析经防抖器在后台线程异步执行，等待结果回填
        await WaitForParseResultAsync(viewModel);

        Assert.Contains("\"alg\": \"HS256\"", viewModel.HeaderText);
        Assert.Contains("\"name\": \"John Doe\"", viewModel.PayloadText);
        Assert.Empty(messageService.Messages);
    }

    [Fact]
    public void TextEditorBinding_ShouldUseEmptyDefaultText()
    {
        Assert.Equal(string.Empty, TextEditorBinding.TextProperty.GetDefaultValue(typeof(AvaloniaEdit.TextEditor)));
    }

    [Fact]
    public async Task JwtTextEditor_ShouldUpdateViewModelInput()
    {
        await using var session = Avalonia.Headless.HeadlessUnitTestSession.StartNew(typeof(JwtTestApplication));
        var messageService = new TestMessageService();
        var viewModel = new JwtEncodePageViewModel(messageService, TimeSpan.Zero);
        Window? window = null;

        await session.Dispatch(() =>
        {
            var view = new AzrngTools.Views.Encode.JwtEncodePageView
            {
                DataContext = viewModel
            };
            window = new Avalonia.Controls.Window { Content = view };
            window.Show();
            window.UpdateLayout();

            var editor = view.GetVisualDescendants().OfType<AvaloniaEdit.TextEditor>().Single();
            editor.Text = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";
        }, CancellationToken.None);

        // 解析经防抖器在后台线程异步执行，等待结果回填
        await WaitForParseResultAsync(viewModel);

        Assert.NotEmpty(viewModel.Original);
        Assert.Contains("\"alg\": \"HS256\"", viewModel.HeaderText);
        Assert.Contains("\"name\": \"John Doe\"", viewModel.PayloadText);
        Assert.Empty(messageService.Messages);

        await session.Dispatch(() => window!.Close(), CancellationToken.None);
    }

    private static async Task WaitForParseResultAsync(JwtEncodePageViewModel viewModel, int timeoutMilliseconds = 2000)
    {
        var deadline = Environment.TickCount64 + timeoutMilliseconds;
        while (string.IsNullOrWhiteSpace(viewModel.HeaderText) ||
               string.IsNullOrWhiteSpace(viewModel.PayloadText))
        {
            if (Environment.TickCount64 > deadline)
            {
                throw new TimeoutException("等待 JWT 解析结果超时");
            }

            await Task.Delay(20);
        }
    }

    private sealed class JwtTestApplication : Avalonia.Application
    {
    }
}

[CollectionDefinition("AvaloniaHeadlessRegression", DisableParallelization = true)]
public sealed class AvaloniaHeadlessRegressionCollection
{
}
