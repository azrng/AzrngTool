using Avalonia.Threading;
using AzrngTools.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AzrngTools;

internal sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        RegisterAppDomainExceptionHandler();

        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
            TryApplyPreparedUpdateOnExit();
        }
        catch (Exception ex)
        {
            LocalLogHelper.WriteMyLogs("启动异常", ex.GetExceptionAndStack());
            throw;
        }
    }

    /// <summary>
    /// 退出时静默应用已就绪的更新包（只替换文件不重启），下次启动即为新版本；
    /// 手动“立即更新”已发起过替换时由 coordinator 内部标记跳过。
    /// </summary>
    private static void TryApplyPreparedUpdateOnExit()
    {
        try
        {
            var app = App.Current;
            if (app is null)
            {
                return;
            }

            var coordinator = app.Services.GetService<IAppUpdateCoordinatorService>();
            if (coordinator is null)
            {
                return;
            }

            // UI 调度器已停转，放线程池执行避免异步续延死锁；限时等待，超时放弃本轮（缓存包下次启动仍会重试）
            Task.Run(coordinator.TryApplyPreparedUpdateOnExit).Wait(TimeSpan.FromSeconds(10));
        }
        catch (Exception ex)
        {
            LocalLogHelper.WriteMyLogs("退出时应用更新", ex.GetExceptionAndStack());
        }
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    internal static void RegisterUiThreadExceptionHandler()
    {
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            e.Handled = true;
            LocalLogHelper.WriteMyLogs("UI线程未处理异常", e.Exception.GetExceptionAndStack());
        };
    }

    private static void RegisterAppDomainExceptionHandler()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is not Exception exception)
            {
                return;
            }

            LocalLogHelper.WriteMyLogs("未处理异常", exception.GetExceptionAndStack());
        };
    }
}
