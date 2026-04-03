using Avalonia;
using System;

namespace SmartSQL.UI;

/// <summary>
/// 应用程序入口点
/// </summary>
sealed class Program
{
    /// <summary>
    /// 应用程序主入口点
    /// Initialization code. Don't use any Avalonia, third-party APIs or any
    /// SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    /// yet and stuff might break.
    /// </summary>
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    /// <summary>
    /// Avalonia 配置
    /// Avalonia configuration, don't remove; also used by visual designer.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()                                    // 自动检测操作系统平台
            .WithInterFont()                                        // 使用 Inter 字体
            .LogToTrace();                                         // 输出日志到跟踪
}
