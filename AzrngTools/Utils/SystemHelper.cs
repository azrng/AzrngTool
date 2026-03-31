using Avalonia.Controls;
using System.Runtime.InteropServices;

namespace AzrngTools.Utils
{
    public class SystemHelper
    {
        /// <summary>
        /// 是否是windows
        /// </summary>
        public static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        /// <summary>
        /// 是否是osx
        /// </summary>
        public static readonly bool IsOsx = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        public static PixelPoint GetWindowPosition(Window window)
        {
            if (!IsOsx || !window.FrameSize.HasValue)
                return window.Position;
            else
            {
                var yOffset = (int)(window.FrameSize.Value.Height - window.ClientSize.Height);
                return new PixelPoint(window.Position.X, window.Position.Y + yOffset);
            }
        }
    }
}