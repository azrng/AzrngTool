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

    [Fact]
    public void CompressEscapeJsonCommand_ShouldNotifyWhenInputIsAlreadyEscapedJson()
    {
        var messageService = new TestMessageService();
        const string original = """{\"$schema\":\"http://json-schema.org/draft-07/schema#\",\"type\":\"object\"}""";
        var viewModel = new JsonPageViewModel(messageService)
        {
            Original = original
        };

        viewModel.CompressEscapeJsonCommand.Execute(null);

        Assert.Equal(original, viewModel.Original);
        Assert.Single(messageService.Messages);
        Assert.Equal("当前内容看起来已经是转义后的 JSON。请先点击“去除转义”还原，或直接使用当前结果。", messageService.Messages[0].Message);
    }
}
