using AzrngTools.Tests.TestDoubles;
using AzrngTools.Behaviors;
using AzrngTools.ViewModels.Encode;
using Avalonia.VisualTree;

namespace AzrngTools.Tests.ViewModels.Encode;

[Collection("AvaloniaHeadlessRegression")]
public class JwtEncodePageViewModelTests
{
    [Fact]
    public void SettingValidJwt_ShouldDecodeHeaderAndPayload()
    {
        var messageService = new TestMessageService();
        var viewModel = new JwtEncodePageViewModel(messageService)
        {
            Original = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c"
        };

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
        await session.Dispatch(() =>
        {
            var messageService = new TestMessageService();
            var viewModel = new JwtEncodePageViewModel(messageService);
            var view = new AzrngTools.Views.Encode.JwtEncodePageView
            {
                DataContext = viewModel
            };
            var window = new Avalonia.Controls.Window { Content = view };
            window.Show();
            window.UpdateLayout();

            var editor = view.GetVisualDescendants().OfType<AvaloniaEdit.TextEditor>().Single();
            editor.Text = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";

            Assert.NotEmpty(viewModel.Original);
            Assert.Contains("\"alg\": \"HS256\"", viewModel.HeaderText);
            Assert.Contains("\"name\": \"John Doe\"", viewModel.PayloadText);
            Assert.Empty(messageService.Messages);

            window.Close();
        }, CancellationToken.None);
    }

    private sealed class JwtTestApplication : Avalonia.Application
    {
    }
}

[CollectionDefinition("AvaloniaHeadlessRegression", DisableParallelization = true)]
public sealed class AvaloniaHeadlessRegressionCollection
{
}
