using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using AzrngTools.Controls.Database;

namespace AzrngTools.Services.Database;

/// <summary>
/// 消息提示服务
/// 提供成功、错误、警告、信息四种类型的通知
/// </summary>
public static class ToastService
{
    private static Panel? _toastContainer;

    public static Panel? CurrentContainer => _toastContainer;

    /// <summary>
    /// 设置 Toast 容器（需在 MainWindow 加载后调用）
    /// </summary>
    public static Panel? SetContainer(Panel toastContainer)
    {
        var previousContainer = _toastContainer;
        _toastContainer = toastContainer;
        return previousContainer;
    }

    public static void ClearContainer()
    {
        _toastContainer = null;
    }

    /// <summary>
    /// 显示成功消息
    /// </summary>
    public static void ShowSuccess(string message, int autoCloseDelay = 3000)
    {
        ShowToast(message, MessageType.Success, autoCloseDelay);
    }

    /// <summary>
    /// 显示错误消息
    /// </summary>
    public static void ShowError(string message, int autoCloseDelay = 5000)
    {
        ShowToast(message, MessageType.Error, autoCloseDelay);
    }

    /// <summary>
    /// 显示警告消息
    /// </summary>
    public static void ShowWarning(string message, int autoCloseDelay = 4000)
    {
        ShowToast(message, MessageType.Warning, autoCloseDelay);
    }

    /// <summary>
    /// 显示信息消息
    /// </summary>
    public static void ShowInfo(string message, int autoCloseDelay = 3000)
    {
        ShowToast(message, MessageType.Info, autoCloseDelay);
    }

    private static void ShowToast(string message, MessageType type, int autoCloseDelay)
    {
        var targetContainer = _toastContainer;
        if (targetContainer == null)
        {
            LoggingService.LogWarning("Toast 容器未初始化，无法显示消息");
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            var toast = new ToastNotification
            {
                Message = message,
                MessageType = type,
                AutoCloseDelay = autoCloseDelay
            };

            toast.Closed += (s, e) =>
            {
                if (s is ToastNotification t)
                {
                    targetContainer.Children.Remove(t);
                }
            };

            targetContainer.Children.Add(toast);

            // 自动显示
            toast.Show(message, type, autoCloseDelay);
        });
    }
}
