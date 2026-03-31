using Avalonia.Threading;

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
        }
        catch (Exception ex)
        {
            LocalLogHelper.WriteMyLogs("启动异常", ex.GetExceptionAndStack());
            throw;
        }
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
                  .UseWin32()
                  .UseSkia()
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
