namespace AzrngTools.Utils
{
    /// <summary>
    /// 图片帮助类
    /// </summary>
    public class ImageHelper
    {
        // public static Bitmap GetImageFromFile(String path)
        // {
        //     try
        //     {
        //         return new Bitmap(GetImageFullPath(path));
        //     }
        //     catch (Exception)
        //     {
        //         return GetImageFromResources("broken-link.png");
        //     }
        // }

        private static string GetImageFullPath(string fileName)
            => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
    }
}