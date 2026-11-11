using System.Net;
using System.Net.Http;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Markup.Xaml;
using AzrngTools.Services;
using AzrngTools.Services.Network;
using AzrngTools.ViewModels;
using AzrngTools.ViewModels.Encode;
using AzrngTools.ViewModels.Encrypts;
using AzrngTools.ViewModels.Format;
using AzrngTools.ViewModels.Network;
using AzrngTools.ViewModels.Other;
using AzrngTools.ViewModels.Pdf;
using AzrngTools.ViewModels.Setting;
using AzrngTools.ViewModels.TextHandle;
using AzrngTools.Views;
using AzrngTools.Views.Encode;
using AzrngTools.Views.Encrypts;
using AzrngTools.Views.Format;
using AzrngTools.Views.Network;
using AzrngTools.Views.Other;
using AzrngTools.Views.Pdf;
using AzrngTools.Views.Setting;
using AzrngTools.Views.TextHandle;
using GTranslate.Translators;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AzrngTools;

public partial class App : Application
{
    public App()
    {
        Services = ConfigureServices();
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        RequestedThemeVariant = Services.GetRequiredService<IThemePreferenceService>().LoadRequestedThemeVariant();

        ViewLocator.Register<MainWindowViewModel, MainWindow>();
        ViewLocator.Register<OverviewPageViewModel, OverviewPageView>();
        ViewLocator.Register<ApiRequestPageViewModel, ApiRequestPageView>();
        ViewLocator.Register<PdfManagementPageViewModel, PdfManagementPageView>();

        ViewLocator.Register<HashPageViewModel, HashPageView>();
        ViewLocator.Register<AesEncryptPageViewModel, AesEncryptPageView>();
        ViewLocator.Register<DesEncryptPageViewModel, DesEncryptPageView>();
        ViewLocator.Register<HmacHashPageViewModel, HmacHashPageView>();
        ViewLocator.Register<Sm4EncryptPageViewModel, Sm4EncryptPageView>();
        ViewLocator.Register<RsaEncryptPageViewModel, RsaEncryptPageView>();

        ViewLocator.Register<GuidShowPageViewModel, GuidShowPageView>();
        ViewLocator.Register<Base64EncodePageViewModel, Base64EncodePageView>();
        ViewLocator.Register<Base64ToImagePageViewModel, Base64ToImagePageView>();
        ViewLocator.Register<UrlEncodePageViewModel, UrlEncodePageView>();
        ViewLocator.Register<UnicodeEncodePageViewModel, UnicodeEncodePageView>();
        ViewLocator.Register<HexEncodePageViewModel, HexEncodePageView>();
        ViewLocator.Register<ChineseConvertPageViewModel, ChineseConvertPageView>();
        ViewLocator.Register<JsonToCsharpPageViewModel, JsonToCsharpPageView>();
        ViewLocator.Register<PasswordGeneratorPageViewModel, PasswordGeneratorPageView>();
        ViewLocator.Register<UnixTimestampPageViewModel, UnixTimestampPageView>();
        ViewLocator.Register<TranslatorPageViewModel, TranslatorPageView>();

        ViewLocator.Register<JsonPageViewModel, JsonPageView>();
        ViewLocator.Register<SqlFormatPageViewModel, SqlFormatPageView>();
        ViewLocator.Register<XmlToHtmlPageViewModel, XmlToHtmlPageView>();
        ViewLocator.Register<JsonYamlXmlPageViewModel, JsonYamlXmlPageView>();
        ViewLocator.Register<JwtEncodePageViewModel, JwtEncodePageView>();
        ViewLocator.Register<RegexAnalysisViewModel, RegexAnalysisView>();
        ViewLocator.Register<WordCountPageViewModel, WordCountPageView>();
        ViewLocator.Register<RMBConvertPageViewModel, RMBConvertPageView>();
        ViewLocator.Register<MimeQueryPageViewModel, MimeQueryPageView>();

        //ViewLocator.Register<IcoToConvertPageViewModel, IcoToConvertPageView>();
        ViewLocator.Register<GzipEncodePageViewModel, GzipEncodePageView>();
        ViewLocator.Register<EncodePageViewModel, EncodePageView>();
        ViewLocator.Register<MarkdownPageViewModel, MarkdownPageView>();
        ViewLocator.Register<JsonSchemaPageViewModel, JsonSchemaPageView>();
        ViewLocator.Register<AboutPageViewModel, AboutPageView>();
        ViewLocator.Register<StringPageViewModel, StringPageView>();
        ViewLocator.Register<HardwarePageViewModel, HardwarePageView>();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Program.RegisterUiThreadExceptionHandler();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = Services.GetRequiredService<MainWindow>();
            // 退出即取消后台更新链路：流式下载不受 HttpClient.Timeout 约束，必须显式取消才能及时结束
            desktop.Exit += (_, _) => AppLifetimeCts.Cancel();
        }

        _ = Services.GetRequiredService<IAppUpdateCoordinatorService>().EnsureStartupCheckAsync(AppLifetimeCts.Token);

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Gets the current <see cref="App"/> instance in use
    /// </summary>
    public static new App Current => (App)Avalonia.Application.Current!;

    /// <summary>
    /// 通知页面
    /// </summary>
    public static WindowNotificationManager? NotificationPage { get; set; }

    /// <summary>
    /// 应用生命周期取消令牌源：退出时取消后台自动更新等长任务链路
    /// </summary>
    internal static readonly CancellationTokenSource AppLifetimeCts = new();

    /// <summary>
    /// Gets the <see cref="IServiceProvider"/> instance to resolve application services.
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// Configures the services for the application.
    /// </summary>
    private IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 服务集合装配；声明为虚方法供测试覆写——CaptureApp 在其上追加注册，
    /// 把持久化服务替换为临时目录实现，避免测试读写真实用户数据目录
    /// </summary>
    protected virtual void ConfigureServices(ServiceCollection services)
    {
        services.AddLogging(builder => { builder.AddConsole(); });

        services.AddSingleton<MainWindow>();
        services.AddSingleton<IAppInfoService, AppInfoService>();
        services.AddSingleton<IThemePreferenceService, ThemePreferenceService>();
        services.AddSingleton<ITranslator, YandexTranslator>();
        services.AddHttpClient();
        // 接口调试勾选“忽略 SSL 错误”时使用的命名客户端
        services.AddHttpClient(ApiRequestExecutionService.IgnoreSslClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                });
        services.AddHttpClient(nameof(AppUpdateService), client =>
        {
            client.Timeout = TimeSpan.FromSeconds(100);
            client.DefaultRequestVersion = HttpVersion.Version11;
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        });

        // 扫描入口程序集与应用程序集（生产两者相同只扫一份；Headless 测试进程入口是
        // testhost，补扫应用自身程序集才能注册全部业务服务）
        var entry = AssemblyHelper.GetEntryAssembly();
        var appAssembly = typeof(App).Assembly;
        services.RegisterBusinessServices(entry is null || entry == appAssembly ? appAssembly : entry, appAssembly);

        // 注入ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<OverviewPageViewModel>();
        services.AddTransient<ApiRequestPageViewModel>();
        services.AddTransient<PdfManagementPageViewModel>();

        services.AddTransient<HashPageViewModel>();
        services.AddTransient<AesEncryptPageViewModel>();
        services.AddTransient<DesEncryptPageViewModel>();
        services.AddTransient<HmacHashPageViewModel>();
        services.AddTransient<Sm4EncryptPageViewModel>();
        services.AddTransient<RsaEncryptPageViewModel>();

        services.AddTransient<GuidShowPageViewModel>();
        services.AddTransient<JsonSchemaPageViewModel>();
        services.AddTransient<JsonToCsharpPageViewModel>();
        services.AddTransient<PasswordGeneratorPageViewModel>();
        services.AddTransient<UnixTimestampPageViewModel>();
        services.AddTransient<Base64EncodePageViewModel>();
        services.AddTransient<Base64ToImagePageViewModel>();
        services.AddTransient<UrlEncodePageViewModel>();
        services.AddTransient<UnicodeEncodePageViewModel>();
        services.AddTransient<HexEncodePageViewModel>();
        services.AddTransient<ChineseConvertPageViewModel>();
        services.AddTransient<TranslatorPageViewModel>();

        services.AddTransient<StringPageViewModel>();
        services.AddTransient<JsonPageViewModel>();
        services.AddTransient<SqlFormatPageViewModel>();
        services.AddTransient<XmlToHtmlPageViewModel>();
        services.AddTransient<JsonYamlXmlPageViewModel>();
        services.AddTransient<JwtEncodePageViewModel>();
        services.AddTransient<RegexAnalysisViewModel>();
        services.AddTransient<WordCountPageViewModel>();
        services.AddTransient<RMBConvertPageViewModel>();
        services.AddTransient<MimeQueryPageViewModel>();

        // services.AddTransient<IcoToConvertPageViewModel>();
        services.AddTransient<GzipEncodePageViewModel>();
        services.AddTransient<EncodePageViewModel>();
        services.AddTransient<MarkdownPageViewModel>();
        services.AddTransient<AboutPageViewModel>();
        services.AddTransient<HardwarePageViewModel>();
    }
}
