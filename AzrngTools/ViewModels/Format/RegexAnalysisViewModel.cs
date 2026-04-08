#nullable disable
using AzrngTools.Utils.Events;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Text;
using System.Text.RegularExpressions;

namespace AzrngTools.ViewModels.Format;

/// <summary>
/// 正则表达式测试
/// </summary>
public partial class RegexAnalysisViewModel : ViewModelBase
{
    private readonly IMessageService _messageService;

    public RegexAnalysisViewModel(IMessageService messageService)
    {
        _messageService = messageService;
        _regexPattern = @"\b\w+@\w+\.\w+\b";
        _testText = "test@example.com\nhello world\nfoo@bar.com";
        _replacementText = "[EMAIL]";
    }

    [ObservableProperty]
    private string _regexPattern;

    [ObservableProperty]
    private string _testText;

    [ObservableProperty]
    private string _replacementText;

    [ObservableProperty]
    private bool _ignoreCase;

    [ObservableProperty]
    private bool _multiline = true;

    [ObservableProperty]
    private bool _singleline;

    [ObservableProperty]
    private string _highlightedText = string.Empty;

    [ObservableProperty]
    private string _matchSummary = string.Empty;

    [ObservableProperty]
    private string _groupDetails = string.Empty;

    [ObservableProperty]
    private string _replacedText = string.Empty;

    [RelayCommand]
    private void TestRegex()
    {
        try
        {
            if (RegexPattern.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入正则表达式");
                return;
            }

            if (TestText.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入测试文本");
                return;
            }

            var regex = BuildRegex();
            var matches = regex.Matches(TestText);

            HighlightedText = BuildHighlightedText(TestText, matches);
            MatchSummary = BuildMatchSummary(matches);
            GroupDetails = BuildGroupDetails(matches);

            _messageService.SendMessage($"匹配完成，共 {matches.Count} 项");
        }
        catch (ArgumentException ex)
        {
            _messageService.SendMessage($"正则表达式无效：{ex.Message}");
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"匹配失败：{ex.Message}");
        }
    }

    [RelayCommand]
    private void ReplaceText()
    {
        try
        {
            if (RegexPattern.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入正则表达式");
                return;
            }

            if (TestText.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入测试文本");
                return;
            }

            var regex = BuildRegex();
            ReplacedText = regex.Replace(TestText, ReplacementText);
            _messageService.SendMessage("替换完成");
        }
        catch (ArgumentException ex)
        {
            _messageService.SendMessage($"正则表达式无效：{ex.Message}");
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"替换失败：{ex.Message}");
        }
    }

    [RelayCommand]
    private void Clear()
    {
        RegexPattern = string.Empty;
        TestText = string.Empty;
        ReplacementText = string.Empty;
        HighlightedText = string.Empty;
        MatchSummary = string.Empty;
        GroupDetails = string.Empty;
        ReplacedText = string.Empty;
    }

    [RelayCommand]
    private void LoadExample()
    {
        RegexPattern = @"\b\w+@\w+\.\w+\b";
        TestText = "test@example.com\nhello world\nfoo@bar.com";
        ReplacementText = "[EMAIL]";
        HighlightedText = string.Empty;
        MatchSummary = string.Empty;
        GroupDetails = string.Empty;
        ReplacedText = string.Empty;
    }

    private Regex BuildRegex()
    {
        var options = RegexOptions.None;
        if (IgnoreCase)
        {
            options |= RegexOptions.IgnoreCase;
        }

        if (Multiline)
        {
            options |= RegexOptions.Multiline;
        }

        if (Singleline)
        {
            options |= RegexOptions.Singleline;
        }

        return new Regex(RegexPattern, options);
    }

    private static string BuildHighlightedText(string text, MatchCollection matches)
    {
        if (matches.Count == 0)
        {
            return text;
        }

        var builder = new StringBuilder();
        var currentIndex = 0;

        foreach (Match match in matches)
        {
            if (!match.Success)
            {
                continue;
            }

            if (match.Index > currentIndex)
            {
                builder.Append(text[currentIndex..match.Index]);
            }

            builder.Append("【");
            builder.Append(match.Value);
            builder.Append("】");
            currentIndex = match.Index + match.Length;
        }

        if (currentIndex < text.Length)
        {
            builder.Append(text[currentIndex..]);
        }

        return builder.ToString();
    }

    private static string BuildMatchSummary(MatchCollection matches)
    {
        if (matches.Count == 0)
        {
            return "未匹配到任何内容";
        }

        var builder = new StringBuilder();
        builder.AppendLine($"匹配数量：{matches.Count}");

        var index = 1;
        foreach (Match match in matches)
        {
            if (!match.Success)
            {
                continue;
            }

            builder.AppendLine($"[{index}] 位置：{match.Index}，长度：{match.Length}，内容：{match.Value}");
            index++;
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildGroupDetails(MatchCollection matches)
    {
        if (matches.Count == 0)
        {
            return "无分组信息";
        }

        var builder = new StringBuilder();
        var matchIndex = 1;

        foreach (Match match in matches)
        {
            if (!match.Success)
            {
                continue;
            }

            builder.AppendLine($"匹配 {matchIndex}：");
            for (var groupIndex = 0; groupIndex < match.Groups.Count; groupIndex++)
            {
                var group = match.Groups[groupIndex];
                builder.AppendLine($"  组 {groupIndex} -> 位置：{group.Index}，长度：{group.Length}，内容：{group.Value}");
            }

            matchIndex++;
        }

        return builder.ToString().TrimEnd();
    }
}
