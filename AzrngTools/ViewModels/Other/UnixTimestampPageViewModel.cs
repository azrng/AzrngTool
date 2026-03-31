using AzrngTools.Utils;
using AzrngTools.Utils.Events;
using Avalonia.Controls;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AzrngTools.ViewModels.Other;

/// <summary>
/// Unix时间戳转换
/// </summary>
public partial class UnixTimestampPageViewModel : ViewModelBase
{
    private readonly IMessageService _messageService;
    private bool _syncingTargetSelection;

    public UnixTimestampPageViewModel(IMessageService messageService)
    {
        _messageService = messageService;
        RefreshCurrentTimestampValues();
        TargetDateTime = DateTime.Now;
        UpdateTargetTimestampOutputs(TargetDateTime);
        TimestampSeconds = TargetTimestampSeconds;
        TimestampMilliseconds = TargetTimestampMilliseconds;
        ConversionResult = string.Empty;
    }

    #region 属性

    [ObservableProperty]
    private string _currentTimestamp;

    [ObservableProperty]
    private string _currentTimestampSeconds;

    [ObservableProperty]
    private string _currentTimestampMilliseconds;

    [ObservableProperty]
    private string _timestampSeconds;

    [ObservableProperty]
    private string _timestampMilliseconds;

    [ObservableProperty]
    private DateTime _targetDateTime;

    [ObservableProperty]
    private DateTime? _targetDateSelection;

    [ObservableProperty]
    private TimeSpan? _targetTimeSelection;

    [ObservableProperty]
    private string _targetTimestampSeconds;

    [ObservableProperty]
    private string _targetTimestampMilliseconds;

    [ObservableProperty]
    private string _conversionResult;

    #endregion

    partial void OnTargetDateTimeChanged(DateTime value)
    {
        UpdateTargetTimestampOutputs(value);

        if (_syncingTargetSelection)
        {
            return;
        }

        _syncingTargetSelection = true;
        TargetDateSelection = value.Date;
        TargetTimeSelection = value.TimeOfDay;
        _syncingTargetSelection = false;
    }

    partial void OnTargetDateSelectionChanged(DateTime? value)
    {
        if (_syncingTargetSelection || value is null)
        {
            return;
        }

        _syncingTargetSelection = true;
        TargetDateTime = value.Value.Date + (TargetTimeSelection ?? TargetDateTime.TimeOfDay);
        _syncingTargetSelection = false;
    }

    partial void OnTargetTimeSelectionChanged(TimeSpan? value)
    {
        if (_syncingTargetSelection || value is null)
        {
            return;
        }

        _syncingTargetSelection = true;
        TargetDateTime = TargetDateTime.Date + value.Value;
        _syncingTargetSelection = false;
    }

    private void RefreshCurrentTimestampValues()
    {
        var (seconds, milliseconds) = GetUnixTimestampValues(DateTime.Now);
        CurrentTimestampSeconds = seconds.ToString();
        CurrentTimestampMilliseconds = milliseconds.ToString();
        CurrentTimestamp = $"秒级：{CurrentTimestampSeconds}\r\n毫秒级：{CurrentTimestampMilliseconds}";
    }

    private void UpdateTargetTimestampOutputs(DateTime value)
    {
        var (seconds, milliseconds) = GetUnixTimestampValues(value);
        TargetTimestampSeconds = seconds.ToString();
        TargetTimestampMilliseconds = milliseconds.ToString();
    }

    private static (long Seconds, long Milliseconds) GetUnixTimestampValues(DateTime value)
    {
        var targetUtc = value.ToUniversalTime();
        var unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var duration = targetUtc - unixEpoch;

        return ((long)duration.TotalSeconds, (long)duration.TotalMilliseconds);
    }

    [RelayCommand]
    private void RefreshCurrentTimestamp()
    {
        RefreshCurrentTimestampValues();
    }

    [RelayCommand]
    private void TimestampToDateTimeSeconds()
    {
        try
        {
            if (TimestampSeconds.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入时间戳（秒）");
                return;
            }

            if (!long.TryParse(TimestampSeconds.Trim(), out var timestamp))
            {
                _messageService.SendMessage("时间戳格式不正确");
                return;
            }

            var unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var dateTime = unixEpoch.AddSeconds(timestamp);
            var localTime = dateTime.ToLocalTime();

            ConversionResult = $"时间戳（秒）：{timestamp}\r\n" +
                              $"UTC时间：{dateTime:yyyy-MM-dd HH:mm:ss}\r\n" +
                              $"本地时间：{localTime:yyyy-MM-dd HH:mm:ss}\r\n" +
                              $"星期：{GetWeekday(localTime.DayOfWeek)}";

            _messageService.SendMessage("转换完成");
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"转换失败：{ex.Message}");
        }
    }

    [RelayCommand]
    private void TimestampToDateTimeMilliseconds()
    {
        try
        {
            if (TimestampMilliseconds.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入时间戳（毫秒）");
                return;
            }

            if (!long.TryParse(TimestampMilliseconds.Trim(), out var timestamp))
            {
                _messageService.SendMessage("时间戳格式不正确");
                return;
            }

            var unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var dateTime = unixEpoch.AddMilliseconds(timestamp);
            var localTime = dateTime.ToLocalTime();

            ConversionResult = $"时间戳（毫秒）：{timestamp}\r\n" +
                              $"UTC时间：{dateTime:yyyy-MM-dd HH:mm:ss.fff}\r\n" +
                              $"本地时间：{localTime:yyyy-MM-dd HH:mm:ss.fff}\r\n" +
                              $"星期：{GetWeekday(localTime.DayOfWeek)}";

            _messageService.SendMessage("转换完成");
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"转换失败：{ex.Message}");
        }
    }

    [RelayCommand]
    private void DateTimeToTimestamp()
    {
        try
        {
            var targetUtc = TargetDateTime.ToUniversalTime();
            var (timestampSeconds, timestampMilliseconds) = GetUnixTimestampValues(TargetDateTime);
            TimestampSeconds = timestampSeconds.ToString();
            TimestampMilliseconds = timestampMilliseconds.ToString();

            ConversionResult = $"本地时间：{TargetDateTime:yyyy-MM-dd HH:mm:ss}\r\n" +
                              $"UTC时间：{targetUtc:yyyy-MM-dd HH:mm:ss}\r\n" +
                              $"时间戳（秒）：{timestampSeconds}\r\n" +
                              $"时间戳（毫秒）：{timestampMilliseconds}\r\n" +
                              $"星期：{GetWeekday(TargetDateTime.DayOfWeek)}";

            _messageService.SendMessage("转换完成");
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"转换失败：{ex.Message}");
        }
    }

    private string GetWeekday(DayOfWeek dayOfWeek)
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
            _ => ""
        };
    }

    [RelayCommand]
    private void SetToNow()
    {
        TargetDateTime = DateTime.Now;
        TimestampSeconds = TargetTimestampSeconds;
        TimestampMilliseconds = TargetTimestampMilliseconds;
        _messageService.SendMessage("已设置为当前时间");
    }

    [RelayCommand]
    private void Clear()
    {
        TargetDateTime = DateTime.Now;
        TimestampSeconds = TargetTimestampSeconds;
        TimestampMilliseconds = TargetTimestampMilliseconds;
        ConversionResult = string.Empty;
    }

    [RelayCommand]
    private async Task CopyResult()
    {
        try
        {
            if (ConversionResult.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("没有可复制的内容");
                return;
            }

            var topLevel = GetTopLevel();
            if (topLevel != null)
            {
                await topLevel.Clipboard.SetTextAsync(ConversionResult);
            }

            _messageService.SendMessage("已复制到剪贴板");
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"复制失败：{ex.Message}");
        }
    }

    [RelayCommand]
    private async Task CopyCurrentTimestamp()
    {
        try
        {
            var topLevel = GetTopLevel();
            if (topLevel != null)
            {
                await topLevel.Clipboard.SetTextAsync(CurrentTimestamp);
            }

            _messageService.SendMessage("已复制当前时间戳到剪贴板");
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"复制失败：{ex.Message}");
        }
    }

    private TopLevel GetTopLevel()
    {
        return TopLevel.GetTopLevel(App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null);
    }
}
