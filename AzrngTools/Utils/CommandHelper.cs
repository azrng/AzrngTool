using System.Diagnostics;

namespace AzrngTools.Utils
{
    public class CommandHelper
    {
        /// <summary>
        /// 浏览器打开指定网址
        /// </summary>
        /// <param name="link"></param>
        public static void OpenBrowserForVisitSite(string link)
        {
            var param = new ProcessStartInfo { FileName = link, UseShellExecute = true, Verb = "open" };
            Process.Start(param);
        }
    }
}