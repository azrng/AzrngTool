using System.Net;
using System.Net.Http;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Markup.Xaml;
using AzrngTools.Services;
using AzrngTools.ViewModels;
using AzrngTools.ViewModels.Encode;
using AzrngTools.ViewModels.Encrypts;
using AzrngTools.ViewModels.Format;
using AzrngTools.ViewModels.Other;
using AzrngTools.ViewModels.Setting;
using AzrngTools.ViewModels.TextHandle;
using AzrngTools.Views;
using AzrngTools.Views.Database;
using AzrngTools.Views.Encode;
using AzrngTools.Views.Encrypts;
using AzrngTools.Views.Format;
using AzrngTools.Views.Other;
using AzrngTools.Views.Setting;
using AzrngTools.Views.TextHandle;
using GTranslate.Translators;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DbWorkbenchViewModel = AzrngTools.ViewModels.Database.MainWindowViewModel;

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
        ViewLocator.Register<DbWorkbenchViewModel, DatabaseWorkbenchPageView>();

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
        }

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
    /// Gets the <see cref="IServiceProvider"/> instance to resolve application services.
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// Configures the services for the application.
    /// </summary>
    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => { builder.AddConsole(); });

        services.AddSingleton<MainWindow>();
        services.AddSingleton<IAppInfoService, AppInfoService>();
        services.AddSingleton<IThemePreferenceService, ThemePreferenceService>();
        services.AddSingleton<ITranslator, YandexTranslator>();
        services.AddHttpClient();
        services.AddHttpClient(nameof(AppUpdateService), client =>
        {
            client.Timeout = TimeSpan.FromSeconds(100);
            client.DefaultRequestVersion = HttpVersion.Version11;
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        });

        var assembly = AssemblyHelper.GetEntryAssembly() ?? typeof(App).Assembly;
        services.RegisterBusinessServices(assembly);

        // 注入ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<OverviewPageViewModel>();
        services.AddTransient<DbWorkbenchViewModel>();

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

        return services.BuildServiceProvider();
    }
}
