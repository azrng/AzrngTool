#nullable disable
using AzrngTools.Utils;
using AzrngTools.Utils.Events;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;

namespace AzrngTools.ViewModels.Encode;

/// <summary>
/// Base64 转图片
/// </summary>
public partial class Base64ToImagePageViewModel : ViewModelBase
{
    private readonly IMessageService _messageService;

    public Base64ToImagePageViewModel(IMessageService messageService)
    {
        _messageService = messageService;
        _base64String = string.Empty;
    }

    #region 属性

    /// <summary>
    /// Base64 字符串
    /// </summary>
    [ObservableProperty]
    private string _base64String;

    /// <summary>
    /// 图片字节数组
    /// </summary>
    [ObservableProperty]
    private byte[] _imageBytes;

    /// <summary>
    /// 预览图片
    /// </summary>
    [ObservableProperty]
    private Bitmap _previewImage;

    /// <summary>
    /// 是否有图片
    /// </summary>
    [ObservableProperty]
    private bool _hasImage;

    #endregion

    /// <summary>
    /// 转换为图片
    /// </summary>
    [RelayCommand]
    private async Task ConvertToImage()
    {
        try
        {
            if (Base64String.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入 Base64 字符串");
                return;
            }

            var base64 = Base64String.Trim();
            if (base64.Contains(','))
            {
                base64 = base64[(base64.IndexOf(',') + 1)..];
            }

            ImageBytes = await Task.Run(() => Convert.FromBase64String(base64));
            using var stream = new MemoryStream(ImageBytes);
            PreviewImage = new Bitmap(stream);
            HasImage = ImageBytes is { Length: > 0 };

            if (HasImage)
            {
                _messageService.SendMessage($"转换成功，图片大小：{ImageBytes.Length} 字节");
            }
        }
        catch (Exception ex)
        {
            PreviewImage = null;
            ImageBytes = null;
            HasImage = false;
            _messageService.SendMessage($"转换失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 从剪贴板粘贴
    /// </summary>
    [RelayCommand]
    private async Task PasteFromClipboard()
    {
        try
        {
            var topLevel = GetTopLevel();
            if (topLevel == null)
            {
                _messageService.SendMessage("无法获取主窗口");
                return;
            }

            var clipboardText = await ClipboardHelper.GetTextAsync(topLevel);
            if (clipboardText.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("剪贴板中没有可用的 Base64 内容");
                return;
            }

            Base64String = clipboardText.Trim();
            _messageService.SendMessage($"已粘贴 {Base64String.Length} 个字符");
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"粘贴失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 清空
    /// </summary>
    [RelayCommand]
    private void Clear()
    {
        Base64String = string.Empty;
        PreviewImage = null;
        ImageBytes = null;
        HasImage = false;
    }

    /// <summary>
    /// 保存图片
    /// </summary>
    [RelayCommand]
    private async Task SaveImage()
    {
        try
        {
            if (!HasImage || ImageBytes == null)
            {
                _messageService.SendMessage("没有可保存的图片");
                return;
            }

            var topLevel = GetTopLevel();
            if (topLevel == null)
            {
                _messageService.SendMessage("无法获取主窗口");
                return;
            }

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "保存图片",
                DefaultExtension = "png",
                FileTypeChoices = new List<FilePickerFileType>
                {
                    new("PNG 图片") { Patterns = new[] { "*.png" } },
                    new("JPEG 图片") { Patterns = new[] { "*.jpg", "*.jpeg" } },
                    new("BMP 图片") { Patterns = new[] { "*.bmp" } },
                    new("所有文件") { Patterns = new[] { "*.*" } }
                }
            });

            if (file != null)
            {
                await File.WriteAllBytesAsync(file.Path.LocalPath, ImageBytes);
                _messageService.SendMessage($"图片已保存至：{file.Path.LocalPath}");
            }
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"保存失败：{ex.Message}");
        }
    }

    private TopLevel GetTopLevel()
    {
        return TopLevel.GetTopLevel(App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null);
    }
}
