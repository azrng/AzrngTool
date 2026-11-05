#nullable disable
using AzrngTools.Services;
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
    /// <summary>输入记忆存档的页面标识（DES-SETTINGS-002 接入清单）。</summary>
    private const string PageId = "other-unix-timestamp";

    private readonly IMessageService _messageService;
    private readonly IPageInputStoreService _pageInputStore;
    private bool _syncingTargetSelection;

    // 构造恢复期间的属性赋值会再次触发 OnXxxChanged，置位跳过保存提交避免恢复即回写
    private bool _isRestoring;

    public UnixTimestampPageViewModel(IMessageService messageService, IPageInputStoreService pageInputStore)
    {
        _messageService = messageService;
        _pageInputStore = pageInputStore;
        // 构造初始化赋值不属于用户输入，置位跳过保存提交，避免默认值快照在防抖落盘时覆盖已有存档
        _isRestoring = true;
        RefreshCurrentTimestampValues();
        TargetDateTime = DateTime.Now;
        UpdateTargetTimestampOutputs(TargetDateTime);
        TimestampSeconds = TargetTimestampSeconds;
        TimestampMilliseconds = TargetTimestampMilliseconds;
        ConversionResult = string.Empty;
        _isRestoring = false;
        RestoreInputs();
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

    partial void OnTimestampSecondsChanged(string value)
    {
        if (_isRestoring)
        {
            return;
        }

        SaveInputs();
    }

    partial void OnTimestampMillisecondsChanged(string value)
    {
        if (_isRestoring)
        {
            return;
        }

        SaveInputs();
    }

    partial void OnTargetDateTimeChanged(DateTime value)
    {
        // 恢复期间保留时间戳输出与日期时间选择联动，仅跳过保存提交避免恢复即回写
        if (!_isRestoring)
        {
            SaveInputs();
        }

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
        // 恢复期间保留日期选择联动，仅跳过保存提交避免恢复即回写
        if (!_isRestoring)
        {
            SaveInputs();
        }

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
        // 恢复期间保留时间选择联动，仅跳过保存提交避免恢复即回写
        if (!_isRestoring)
        {
            SaveInputs();
        }

        if (_syncingTargetSelection || value is null)
        {
            return;
        }

        _syncingTargetSelection = true;
        TargetDateTime = TargetDateTime.Date + value.Value;
        _syncingTargetSelection = false;
    }

    // TargetTimeSelection 为 TimeSpan?，无现成编解码，按 ticks 经 Encode(long)/DecodeInt64 存取
    private void SaveInputs()
    {
        _pageInputStore.Save(PageId, new Dictionary<string, string?>
        {
            [nameof(TimestampSeconds)] = PageInputValues.Encode(TimestampSeconds),
            [nameof(TimestampMilliseconds)] = PageInputValues.Encode(TimestampMilliseconds),
            [nameof(TargetDateTime)] = PageInputValues.Encode(TargetDateTime),
            [nameof(TargetDateSelection)] = TargetDateSelection.HasValue ? PageInputValues.Encode(TargetDateSelection.Value) : null,
            [nameof(TargetTimeSelection)] = TargetTimeSelection.HasValue ? PageInputValues.Encode(TargetTimeSelection.Value.Ticks) : null,
        });
    }

    private void RestoreInputs()
    {
        var saved = _pageInputStore.TryLoad(PageId);
        if (saved.Count == 0)
        {
            return;
        }

        _isRestoring = true;
        try
        {
            if (saved.TryGetValue(nameof(TimestampSeconds), out var timestampSeconds))
            {
                TimestampSeconds = timestampSeconds;
            }

            if (saved.TryGetValue(nameof(TimestampMilliseconds), out var timestampMilliseconds))
            {
                TimestampMilliseconds = timestampMilliseconds;
            }

            // 先恢复 TargetDateTime 触发既有联动同步两个选择，再按存档值覆盖可空选择
            if (saved.TryGetValue(nameof(TargetDateTime), out var targetDateTime))
            {
                TargetDateTime = PageInputValues.DecodeDateTime(targetDateTime, TargetDateTime);
            }

            if (saved.TryGetValue(nameof(TargetDateSelection), out var targetDateSelection))
            {
                // 键存在但值为 null 时按可空默认（null）处理
                TargetDateSelection = targetDateSelection is null
                    ? null
                    : PageInputValues.DecodeDateTime(targetDateSelection, TargetDateSelection ?? TargetDateTime.Date);
            }

            if (saved.TryGetValue(nameof(TargetTimeSelection), out var targetTimeSelection))
            {
                TargetTimeSelection = targetTimeSelection is null
                    ? null
                    : TimeSpan.FromTicks(PageInputValues.DecodeInt64(targetTimeSelection, (TargetTimeSelection ?? TimeSpan.Zero).Ticks));
            }
        }
        finally
        {
            _isRestoring = false;
        }
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
            AppLog.LogError($"秒级时间戳转日期失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
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
            AppLog.LogError($"毫秒级时间戳转日期失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
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
            AppLog.LogError($"日期转时间戳失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
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
                await ClipboardHelper.SetTextAsync(topLevel, ConversionResult);
            }

            _messageService.SendMessage("已复制到剪贴板");
        }
        catch (Exception ex)
        {
            AppLog.LogError($"复制时间戳转换结果失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
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
                await ClipboardHelper.SetTextAsync(topLevel, CurrentTimestamp);
            }

            _messageService.SendMessage("已复制当前时间戳到剪贴板");
        }
        catch (Exception ex)
        {
            AppLog.LogError($"复制当前时间戳失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
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
