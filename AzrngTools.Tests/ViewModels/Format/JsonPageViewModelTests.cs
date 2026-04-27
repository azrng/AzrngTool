using AzrngTools.Tests.TestDoubles;
using AzrngTools.ViewModels.Format;

namespace AzrngTools.Tests.ViewModels.Format;

public class JsonPageViewModelTests
{
    [Fact]
    public void ReplaceEscapeCommand_ShouldFormatEscapedJson()
    {
        var messageService = new TestMessageService();
        var viewModel = new JsonPageViewModel(messageService)
        {
            Original = """{\"name\":\"Azrng\",\"enabled\":true}"""
        };

        viewModel.ReplaceEscapeCommand.Execute(null);

        Assert.Contains(Environment.NewLine + "  \"name\": \"Azrng\",", viewModel.Original);
        Assert.Empty(messageService.Messages);
    }

    [Fact]
    public void ReplaceEscapeCommand_ShouldKeepOriginalTextWhenProcessingFails()
    {
        var messageService = new TestMessageService();
        const string original = "{\\\"name\\\":\\q}";
        var viewModel = new JsonPageViewModel(messageService)
        {
            Original = original
        };

        viewModel.ReplaceEscapeCommand.Execute(null);

        Assert.Equal(original, viewModel.Original);
        Assert.Single(messageService.Messages);
        Assert.StartsWith("处理失败：", messageService.Messages[0].Message);
    }

    [Fact]
    public void CompressJsonCommand_ShouldNotifyWhenInputIsEmpty()
    {
        var messageService = new TestMessageService();
        var viewModel = new JsonPageViewModel(messageService)
        {
            Original = string.Empty
        };

        viewModel.CompressJsonCommand.Execute(null);

        Assert.Single(messageService.Messages);
        Assert.Equal("请输入要处理的内容", messageService.Messages[0].Message);
    }
}
