using Avalonia.Media;
using Avalonia.Media.Fonts;
using System.Diagnostics.CodeAnalysis;

namespace AzrngTools.Utils
{
    /// <summary>
    /// 字体文件配置
    /// </summary>
    public static class FontSettings
    {
        public static Uri Key { get; } = new Uri("fonts:", UriKind.Absolute);
        public static Uri Source { get; } = new Uri("avares://WHOrigin.FrontDesk/Assets/Fonts", UriKind.Absolute);
    }

    public static class AlibabaFontSettings
    {
        public static Uri Key { get; } = new Uri("fonts:AlibabaPuHuiTi", UriKind.Absolute);
        public static Uri Source { get; } = new Uri("avares://WHOrigin.FrontDesk/Assets/Fonts/AliBaba", UriKind.Absolute);
    }

    public static class LcdFontSettings
    {
        public static Uri Key { get; } = new Uri("fonts:LCD", UriKind.Absolute);
        public static Uri Source { get; } = new Uri("avares://WHOrigin.FrontDesk/Assets/Fonts/LCD", UriKind.Absolute);
    }

    public static class Extensions
    {
        public static AppBuilder UseFontFamily([DisallowNull] this AppBuilder builder)
        {
            builder.With(new FontManagerOptions
            {
                DefaultFamilyName = "avares://WHOrigin.FrontDesk/Assets/Fonts/AliBaba/AlibabaPuHuiTi-Regular.ttf#Alibaba PuHuiTi 2.0",
                FontFallbacks = new[]
                {
                    new  FontFallback
                    {
                        FontFamily = new FontFamily("avares://WHOrigin.FrontDesk/Assets/Fonts/AliBaba/AlibabaPuHuiTi-Regular.ttf#Alibaba PuHuiTi 2.0")
                    }
                }
            });
            return builder.ConfigureFonts(manager => manager.AddFontCollection(new EmbeddedFontCollection(FontSettings.Key, FontSettings.Source)))
                          //.ConfigureFonts(manager => manager.AddFontCollection(new EmbeddedFontCollection(AlibabaFontSettings.Key, AlibabaFontSettings.Source)))
                          //.ConfigureFonts(manager => manager.AddFontCollection(new EmbeddedFontCollection(LcdFontSettings.Key, LcdFontSettings.Source)));
                          ;
        }
    }
}