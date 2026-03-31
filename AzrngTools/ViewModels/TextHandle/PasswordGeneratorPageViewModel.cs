using Azrng.Core.Helpers;
using AzrngTools.Utils;
using AzrngTools.Utils.Events;
using Avalonia.Controls;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AzrngTools.ViewModels.TextHandle;

/// <summary>
/// 密码生成器
/// </summary>
public partial class PasswordGeneratorPageViewModel : ViewModelBase
{
    private readonly IMessageService _messageService;

    public PasswordGeneratorPageViewModel(IMessageService messageService)
    {
        _messageService = messageService;
        _passwordLength = 16;
        _includeNumbers = true;
        _includeLowercase = true;
        _includeUppercase = true;
        _includeSpecialChars = false;
        _generatedPasswords = new System.Collections.ObjectModel.ObservableCollection<string>();
        _batchCount = 1;
    }

    #region 属性

    /// <summary>
    /// 生成的密码
    /// </summary>
    [ObservableProperty]
    private string _generatedPassword;

    /// <summary>
    /// 密码长度
    /// </summary>
    [ObservableProperty]
    private int _passwordLength;

    /// <summary>
    /// 包含数字
    /// </summary>
    [ObservableProperty]
    private bool _includeNumbers;

    /// <summary>
    /// 包含小写字母
    /// </summary>
    [ObservableProperty]
    private bool _includeLowercase;

    /// <summary>
    /// 包含大写字母
    /// </summary>
    [ObservableProperty]
    private bool _includeUppercase;

    /// <summary>
    /// 包含特殊字符
    /// </summary>
    [ObservableProperty]
    private bool _includeSpecialChars;

    /// <summary>
    /// 批量生成的密码列表
    /// </summary>
    [ObservableProperty]
    private System.Collections.ObjectModel.ObservableCollection<string> _generatedPasswords;

    /// <summary>
    /// 批量生成数量
    /// </summary>
    [ObservableProperty]
    private int _batchCount;

    /// <summary>
    /// 密码强度
    /// </summary>
    [ObservableProperty]
    private string _passwordStrength;

    #endregion

    /// <summary>
    /// 生成密码
    /// </summary>
    [RelayCommand]
    private void GeneratePassword()
    {
        try
        {
            if (!IncludeNumbers && !IncludeLowercase && !IncludeUppercase && !IncludeSpecialChars)
            {
                _messageService.SendMessage("请至少选择一种字符类型");
                return;
            }

            if (PasswordLength < 4)
            {
                _messageService.SendMessage("密码长度不能小于4");
                return;
            }

            if (PasswordLength > 128)
            {
                _messageService.SendMessage("密码长度不能大于128");
                return;
            }

            GeneratedPassword = RandomGenerator.GenerateVerifyCode(
                PasswordLength,
                IncludeNumbers,
                IncludeUppercase,
                IncludeLowercase,
                IncludeSpecialChars
            );

            // 计算密码强度
            PasswordStrength = CalculatePasswordStrength();
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"生成失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 批量生成密码
    /// </summary>
    [RelayCommand]
    private void GenerateBatchPasswords()
    {
        try
        {
            if (!IncludeNumbers && !IncludeLowercase && !IncludeUppercase && !IncludeSpecialChars)
            {
                _messageService.SendMessage("请至少选择一种字符类型");
                return;
            }

            if (PasswordLength < 4)
            {
                _messageService.SendMessage("密码长度不能小于4");
                return;
            }

            if (BatchCount < 1 || BatchCount > 100)
            {
                _messageService.SendMessage("批量生成数量应在1-100之间");
                return;
            }

            GeneratedPasswords.Clear();
            for (var i = 0; i < BatchCount; i++)
            {
                var password = RandomGenerator.GenerateVerifyCode(
                    PasswordLength,
                    IncludeNumbers,
                    IncludeUppercase,
                    IncludeLowercase,
                    IncludeSpecialChars
                );
                GeneratedPasswords.Add(password);
            }

            _messageService.SendMessage($"成功生成 {BatchCount} 个密码");
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"批量生成失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 生成随机数
    /// </summary>
    [RelayCommand]
    private void GenerateRandomNumber()
    {
        try
        {
            var random = new System.Random();
            var number = random.Next(0, 1000000);
            GeneratedPassword = number.ToString();

            _messageService.SendMessage($"生成随机数：{number}");
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"生成失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 生成随机字符串
    /// </summary>
    [RelayCommand]
    private void GenerateRandomString()
    {
        try
        {
            if (PasswordLength < 1)
            {
                _messageService.SendMessage("长度不能小于1");
                return;
            }

            GeneratedPassword = RandomGenerator.GenerateString(PasswordLength);
            _messageService.SendMessage($"生成随机字符串：{GeneratedPassword}");
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"生成失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 计算密码强度
    /// </summary>
    private string CalculatePasswordStrength()
    {
        if (string.IsNullOrEmpty(GeneratedPassword))
            return "未生成";

        var score = 0;

        // 长度评分
        if (GeneratedPassword.Length >= 8) score++;
        if (GeneratedPassword.Length >= 12) score++;
        if (GeneratedPassword.Length >= 16) score++;

        // 字符类型评分
        if (IncludeNumbers) score++;
        if (IncludeLowercase) score++;
        if (IncludeUppercase) score++;
        if (IncludeSpecialChars) score++;

        // 复杂度评分
        if (GeneratedPassword.Any(char.IsDigit) && GeneratedPassword.Any(char.IsLetter)) score++;
        if (GeneratedPassword.Any(char.IsLower) && GeneratedPassword.Any(char.IsUpper)) score++;

        return score switch
        {
            <= 2 => "弱",
            <= 4 => "较弱",
            <= 6 => "中等",
            <= 8 => "较强",
            _ => "强"
        };
    }

    /// <summary>
    /// 复制密码
    /// </summary>
    [RelayCommand]
    private async Task CopyPassword()
    {
        try
        {
            if (GeneratedPassword.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("没有可复制的内容");
                return;
            }

            var topLevel = GetTopLevel();
            if (topLevel != null)
            {
                await topLevel.Clipboard.SetTextAsync(GeneratedPassword);
            }
            _messageService.SendMessage("已复制到剪贴板");
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"复制失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 清空
    /// </summary>
    [RelayCommand]
    private void Clear()
    {
        GeneratedPassword = string.Empty;
        GeneratedPasswords.Clear();
        PasswordStrength = "未生成";
    }

    /// <summary>
    /// 清空批量生成列表
    /// </summary>
    [RelayCommand]
    private void ClearBatch()
    {
        GeneratedPasswords.Clear();
    }

    /// <summary>
    /// 复制所有批量生成的密码
    /// </summary>
    [RelayCommand]
    private async Task CopyAllPasswords()
    {
        try
        {
            if (GeneratedPasswords.Count == 0)
            {
                _messageService.SendMessage("没有可复制的内容");
                return;
            }

            var allPasswords = string.Join("\n", GeneratedPasswords);
            var topLevel = GetTopLevel();
            if (topLevel != null)
            {
                await topLevel.Clipboard.SetTextAsync(allPasswords);
            }
            _messageService.SendMessage($"已复制 {GeneratedPasswords.Count} 个密码到剪贴板");
        }
        catch (Exception ex)
        {
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
}
