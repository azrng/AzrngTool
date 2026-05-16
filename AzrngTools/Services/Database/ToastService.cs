using System;
using Avalonia.Controls.Notifications;
using Ursa.Controls;

namespace AzrngTools.Services.Database;

public static class ToastService
{
    private static WindowToastManager? _manager;

    public static WindowToastManager? CurrentManager => _manager;

    public static WindowToastManager? SetManager(WindowToastManager manager)
    {
        var previous = _manager;
        _manager = manager;
        return previous;
    }

    public static void ClearManager()
    {
        _manager = null;
    }

    public static void ShowSuccess(string message, int autoCloseDelay = 3000)
    {
        ShowToast(message, NotificationType.Success, autoCloseDelay);
    }

    public static void ShowError(string message, int autoCloseDelay = 5000)
    {
        ShowToast(message, NotificationType.Error, autoCloseDelay);
    }

    public static void ShowWarning(string message, int autoCloseDelay = 4000)
    {
        ShowToast(message, NotificationType.Warning, autoCloseDelay);
    }

    public static void ShowInfo(string message, int autoCloseDelay = 3000)
    {
        ShowToast(message, NotificationType.Information, autoCloseDelay);
    }

    private static void ShowToast(string message, NotificationType type, int autoCloseDelay)
    {
        _manager?.Show(new Toast(message, type, TimeSpan.FromMilliseconds(autoCloseDelay)));
    }
}
