using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SmartSQL.UI.Services;
using SmartSQL.UI.ViewModels;
using SmartSQL.UI.Views;
using System;
using System.Threading.Tasks;

namespace SmartSQL.UI;

public partial class App : Application
{
    /// <summary>
    /// 应用程序初始化
    /// </summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// 框架初始化完成后调用
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        System.Diagnostics.Debug.WriteLine("=== OnFrameworkInitializationCompleted 开始 ===");

        // 初始化日志服务
        try
        {
            LoggingService.Initialize();
            LoggingService.LogInfo("应用程序启动");
            System.Diagnostics.Debug.WriteLine("日志服务初始化成功");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"初始化日志服务失败：{ex.Message}");
            Console.WriteLine($"初始化日志服务失败：{ex.Message}");
        }

        // 强制设置黑色主题（已在App.axaml中设置，这里注释掉避免冲突）
        // RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            System.Diagnostics.Debug.WriteLine("检测到 ClassicDesktopStyleApplicationLifetime");
            Console.WriteLine("检测到 ClassicDesktopStyleApplicationLifetime");

            try
            {
                System.Diagnostics.Debug.WriteLine("开始创建 MainWindowViewModel");
                Console.WriteLine("开始创建 MainWindowViewModel");

                // 创建 ViewModel
                var viewModel = new MainWindowViewModel();

                System.Diagnostics.Debug.WriteLine("MainWindowViewModel 创建成功");
                Console.WriteLine("MainWindowViewModel 创建成功");

                // 设置主窗口
                desktop.MainWindow = new MainWindow
                {
                    DataContext = viewModel,
                };

                System.Diagnostics.Debug.WriteLine("MainWindow 创建成功");
                Console.WriteLine("MainWindow 创建成功");

                // 设置 ViewModel 的 MainWindow 引用
                viewModel.MainWindow = desktop.MainWindow;

                // 设置关闭模式（主窗口关闭时退出应用）
                desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

                LoggingService.LogInfo("主窗口初始化完成");
                System.Diagnostics.Debug.WriteLine("主窗口初始化完成");
                Console.WriteLine("主窗口初始化完成");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"主窗口初始化失败：{ex.Message}\n{ex.StackTrace}");
                Console.WriteLine($"主窗口初始化失败：{ex.Message}\n{ex.StackTrace}");
                LoggingService.LogError("主窗口初始化失败", ex);
                ShowFatalError("应用程序初始化失败", ex.Message);
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("未检测到 ClassicDesktopStyleApplicationLifetime");
            Console.WriteLine("未检测到 ClassicDesktopStyleApplicationLifetime");
        }

        base.OnFrameworkInitializationCompleted();
        System.Diagnostics.Debug.WriteLine("=== OnFrameworkInitializationCompleted 完成 ===");
    }

    /// <summary>
    /// 显示致命错误对话框
    /// </summary>
    private void ShowFatalError(string title, string message)
    {
        try
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow != null)
            {
                // 在 UI 线程上显示错误消息框
                Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
                {
                    try
                    {
                        var messageBox = new Views.MessageBox
                        {
                            Title = title,
                            Message = message,
                            Buttons = MessageBoxButtons.OK,
                            DefaultButton = MessageBoxButtonType.OK
                        };

                        await messageBox.ShowDialog(desktop.MainWindow);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"显示消息框失败：{ex.Message}");
                    }
                });
            }
        }
        catch
        {
            // 如果显示消息框失败，至少记录到调试输出
            System.Diagnostics.Debug.WriteLine($"致命错误：{title}\n{message}");
        }
    }
}
