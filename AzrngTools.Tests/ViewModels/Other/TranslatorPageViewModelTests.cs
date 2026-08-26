using AzrngTools.Services;
using AzrngTools.Tests.TestDoubles;
using AzrngTools.ViewModels.Other;

namespace AzrngTools.Tests.ViewModels.Other;

public class TranslatorPageViewModelTests
{
    [Fact]
    public async Task HandlerCommand_ShouldIgnoreEmptyInput()
    {
        var service = new FakeTranslationService();
        var messageService = new TestMessageService();
        var viewModel = new TranslatorPageViewModel(service, messageService);

        await viewModel.HandlerCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsBusy);
        Assert.Empty(service.Requests);
        Assert.Contains(messageService.Messages, message => message.Message == "请输入要转换的内容");
    }

    [Fact]
    public async Task HandlerCommand_ShouldTranslateAndResetBusyState()
    {
        var service = new FakeTranslationService();
        var viewModel = new TranslatorPageViewModel(service, new TestMessageService())
        {
            OriginText = "你好"
        };

        Assert.True(viewModel.CanTranslate);
        await viewModel.HandlerCommand.ExecuteAsync(null);

        Assert.Equal("translated:你好", viewModel.ResultText);
        Assert.Equal(["zh:你好"], service.Requests);
        Assert.False(viewModel.IsBusy);
        Assert.True(viewModel.CanTranslate);
    }

    private sealed class FakeTranslationService : ITranslationService
    {
        public List<string> Requests { get; } = [];

        public Task<string> YandexChineseToEnglishAsync(string chineseText)
        {
            Requests.Add($"zh:{chineseText}");
            return Task.FromResult($"translated:{chineseText}");
        }

        public Task<string> YandexEnglishToChineseAsync(string englishText)
        {
            Requests.Add($"en:{englishText}");
            return Task.FromResult($"translated:{englishText}");
        }
    }
}
