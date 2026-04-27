using AzrngTools.Tests.TestDoubles;
using AzrngTools.ViewModels.Other;

namespace AzrngTools.Tests.ViewModels.Other;

public class UnixTimestampPageViewModelTests
{
    [Fact]
    public void TimestampToDateTimeSecondsCommand_ShouldNotifyWhenInputIsEmpty()
    {
        var messageService = new TestMessageService();
        var viewModel = new UnixTimestampPageViewModel(messageService)
        {
            TimestampSeconds = string.Empty
        };

        viewModel.TimestampToDateTimeSecondsCommand.Execute(null);

        Assert.Single(messageService.Messages);
        Assert.Equal("请输入时间戳（秒）", messageService.Messages[0].Message);
    }

    [Fact]
    public void TimestampToDateTimeSecondsCommand_ShouldConvertUnixTimestamp()
    {
        var messageService = new TestMessageService();
        var viewModel = new UnixTimestampPageViewModel(messageService)
        {
            TimestampSeconds = "0"
        };

        viewModel.TimestampToDateTimeSecondsCommand.Execute(null);

        var expectedUtc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var expectedLocal = expectedUtc.ToLocalTime();

        Assert.Contains("时间戳（秒）：0", viewModel.ConversionResult);
        Assert.Contains($"UTC时间：{expectedUtc:yyyy-MM-dd HH:mm:ss}", viewModel.ConversionResult);
        Assert.Contains($"本地时间：{expectedLocal:yyyy-MM-dd HH:mm:ss}", viewModel.ConversionResult);
        Assert.Contains($"星期：{GetWeekday(expectedLocal.DayOfWeek)}", viewModel.ConversionResult);
        Assert.Equal("转换完成", messageService.Messages[^1].Message);
    }

    [Fact]
    public void DateTimeToTimestampCommand_ShouldUpdateTimestampOutputs()
    {
        var messageService = new TestMessageService();
        var viewModel = new UnixTimestampPageViewModel(messageService);
        var target = DateTime.SpecifyKind(new DateTime(2024, 1, 2, 3, 4, 5), DateTimeKind.Local);
        var expectedUtc = target.ToUniversalTime();
        var unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var expectedSeconds = ((long)(expectedUtc - unixEpoch).TotalSeconds).ToString();
        var expectedMilliseconds = ((long)(expectedUtc - unixEpoch).TotalMilliseconds).ToString();

        viewModel.TargetDateTime = target;

        viewModel.DateTimeToTimestampCommand.Execute(null);

        Assert.Equal(expectedSeconds, viewModel.TimestampSeconds);
        Assert.Equal(expectedMilliseconds, viewModel.TimestampMilliseconds);
        Assert.Contains($"本地时间：{target:yyyy-MM-dd HH:mm:ss}", viewModel.ConversionResult);
        Assert.Contains($"UTC时间：{expectedUtc:yyyy-MM-dd HH:mm:ss}", viewModel.ConversionResult);
        Assert.Equal("转换完成", messageService.Messages[^1].Message);
    }

    [Fact]
    public void TargetSelections_ShouldSyncTargetDateTime()
    {
        var messageService = new TestMessageService();
        var viewModel = new UnixTimestampPageViewModel(messageService);
        var date = new DateTime(2024, 5, 6);
        var time = new TimeSpan(7, 8, 9);

        viewModel.TargetDateSelection = date;
        viewModel.TargetTimeSelection = time;

        Assert.Equal(date.Date + time, viewModel.TargetDateTime);
        Assert.Equal(date, viewModel.TargetDateSelection);
        Assert.Equal(time, viewModel.TargetTimeSelection);
    }

    private static string GetWeekday(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Monday => "星期一",
            DayOfWeek.Tuesday => "星期二",
            DayOfWeek.Wednesday => "星期三",
            DayOfWeek.Thursday => "星期四",
            DayOfWeek.Friday => "星期五",
            DayOfWeek.Saturday => "星期六",
            DayOfWeek.Sunday => "星期日",
            _ => string.Empty
        };
    }
}
