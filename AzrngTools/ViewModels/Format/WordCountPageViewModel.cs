#nullable disable
using Azrng.Core.Helpers;
using AzrngTools.Utils;
using AzrngTools.Utils.Events;
using Avalonia.Controls;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AzrngTools.ViewModels.Format;

/// <summary>
/// 字数统计
/// </summary>
public partial class WordCountPageViewModel : ViewModelBase
{
    private readonly IMessageService _messageService;

    public WordCountPageViewModel(IMessageService messageService)
    {
        _messageService = messageService;
        _inputText = string.Empty;
        _totalCharacters = 0;
        _chineseCharacters = 0;
        _englishCharacters = 0;
        _numbers = 0;
        _punctuation = 0;
        _spaces = 0;
        _lines = 0;
        _words = 0;
        _paragraphs = 0;
    }

    #region 属性

    /// <summary>
    /// 输入文本
    /// </summary>
    [ObservableProperty]
    private string _inputText;

    /// <summary>
    /// 总字符数
    /// </summary>
    [ObservableProperty]
    private int _totalCharacters;

    /// <summary>
    /// 中文字符数
    /// </summary>
    [ObservableProperty]
    private int _chineseCharacters;

    /// <summary>
    /// 英文字符数
    /// </summary>
    [ObservableProperty]
    private int _englishCharacters;

    /// <summary>
    /// 数字数量
    /// </summary>
    [ObservableProperty]
    private int _numbers;

    /// <summary>
    /// 标点符号数量
    /// </summary>
    [ObservableProperty]
    private int _punctuation;

    /// <summary>
    /// 空格数量
    /// </summary>
    [ObservableProperty]
    private int _spaces;

    /// <summary>
    /// 行数
    /// </summary>
    [ObservableProperty]
    private int _lines;

    /// <summary>
    /// 词数（英文）
    /// </summary>
    [ObservableProperty]
    private int _words;

    /// <summary>
    /// 段落数
    /// </summary>
    [ObservableProperty]
    private int _paragraphs;

    #endregion

    /// <summary>
    /// 统计字数
    /// </summary>
    [RelayCommand]
    private async Task CountWordsAsync()
    {
        try
        {
            if (InputText.IsNullOrWhiteSpace())
            {
                ResetCounts();
                _messageService.SendMessage("请输入要统计的文本");
                return;
            }

            var text = InputText;
            // 大文本多趟扫描有可感耗时，移出 UI 线程执行
            var counts = await Task.Run(() => CountStatistics(text));

            TotalCharacters = counts.TotalCharacters;
            ChineseCharacters = counts.ChineseCharacters;
            EnglishCharacters = counts.EnglishCharacters;
            Numbers = counts.Numbers;
            Punctuation = counts.Punctuation;
            Spaces = counts.Spaces;
            Lines = counts.Lines;
            Words = counts.Words;
            Paragraphs = counts.Paragraphs;

            _messageService.SendMessage("统计完成");
        }
        catch (Exception ex)
        {
            LocalLogHelper.LogError($"字数统计失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
            _messageService.SendMessage($"统计失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 单趟扫描统计字符指标，行/词/段落沿用原分割口径
    /// </summary>
    private static WordCounts CountStatistics(string text)
    {
        var counts = new WordCounts { TotalCharacters = text.Length };
        foreach (var c in text)
        {
            if (IsChineseCharacter(c))
            {
                counts.ChineseCharacters++;
            }
            else if (char.IsLetter(c))
            {
                counts.EnglishCharacters++;
            }

            if (char.IsDigit(c))
            {
                counts.Numbers++;
            }

            if (char.IsPunctuation(c))
            {
                counts.Punctuation++;
            }

            if (char.IsWhiteSpace(c))
            {
                counts.Spaces++;
            }
        }

        counts.Lines = text.Split('\n').Length;
        counts.Words = CountEnglishWords(text);
        counts.Paragraphs = CountParagraphs(text);
        return counts;
    }

    private sealed class WordCounts
    {
        public int TotalCharacters { get; set; }
        public int ChineseCharacters { get; set; }
        public int EnglishCharacters { get; set; }
        public int Numbers { get; set; }
        public int Punctuation { get; set; }
        public int Spaces { get; set; }
        public int Lines { get; set; }
        public int Words { get; set; }
        public int Paragraphs { get; set; }
    }

    /// <summary>
    /// 判断是否为中文字符
    /// </summary>
    private static bool IsChineseCharacter(char value)
    {
        return value is >= '\u4E00' and <= '\u9FFF'
            or >= '\u3400' and <= '\u4DBF'
            or >= '\uF900' and <= '\uFAFF';
    }

    /// <summary>
    /// 统计英文单词数
    /// </summary>
    private static int CountEnglishWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        // 简单的单词统计：按空格和标点符号分割
        var words = text.Split(new[] { ' ', '\t', '\n', '\r', '.', ',', ';', ':', '!', '?', '"', '\'', '(', ')', '[', ']', '{', '}', '<', '>', '/', '\\', '|', '-', '_', '+', '=', '*', '&', '^', '%', '$', '#', '@', '~', '`' }, StringSplitOptions.RemoveEmptyEntries);
        return words.Length;
    }

    /// <summary>
    /// 统计段落数
    /// </summary>
    private static int CountParagraphs(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        // 按双换行符分割段落
        var paragraphs = text.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
        return paragraphs.Length;
    }

    /// <summary>
    /// 重置统计
    /// </summary>
    private void ResetCounts()
    {
        TotalCharacters = 0;
        ChineseCharacters = 0;
        EnglishCharacters = 0;
        Numbers = 0;
        Punctuation = 0;
        Spaces = 0;
        Lines = 0;
        Words = 0;
        Paragraphs = 0;
    }

    /// <summary>
    /// 清空
    /// </summary>
    [RelayCommand]
    private void Clear()
    {
        InputText = string.Empty;
        ResetCounts();
    }

    /// <summary>
    /// 复制统计结果
    /// </summary>
    [RelayCommand]
    private async Task CopyResult()
    {
        try
        {
            if (TotalCharacters == 0)
            {
                _messageService.SendMessage("没有可复制的统计结果");
                return;
            }

            var result = GetStatisticsText();
            var topLevel = GetTopLevel();
            if (topLevel != null)
            {
                await ClipboardHelper.SetTextAsync(topLevel, result);
            }
            _messageService.SendMessage("已复制到剪贴板");
        }
        catch (Exception ex)
        {
            LocalLogHelper.LogError($"复制统计结果失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
            _messageService.SendMessage($"复制失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 获取 TopLevel
    /// </summary>
    private TopLevel GetTopLevel()
    {
        return TopLevel.GetTopLevel(App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null);
    }

    /// <summary>
    /// 获取统计结果文本
    /// </summary>
    private string GetStatisticsText()
    {
        return $"字数统计结果：\r\n" +
               $"总字符数：{TotalCharacters}\r\n" +
               $"中文字符数：{ChineseCharacters}\r\n" +
               $"英文字符数：{EnglishCharacters}\r\n" +
               $"数字数量：{Numbers}\r\n" +
               $"标点符号：{Punctuation}\r\n" +
               $"空格数量：{Spaces}\r\n" +
               $"行数：{Lines}\r\n" +
               $"英文词数：{Words}\r\n" +
               $"段落数：{Paragraphs}";
    }
}
