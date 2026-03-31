using Common.Security.Enums;

namespace AzrngTools.Utils
{
    public class EnumValuesHelper
    {
        //public static List<string> OutTypeValues { get; } = Enum.GetValues(typeof(OutType))
        //                                                        .Cast<OutType>()
        //                                                        .Select(e => e.ToString())
        //                                                        .ToList();

        //public static List<string> RSAKeyTypeValues { get; } = Enum.GetValues(typeof(RSAKeyType))
        //                                                           .Cast<RSAKeyType>()
        //                                                           .Select(e => e.ToString())
        //                                                           .ToList();

        public static List<string> OutTypeValues { get; } = new List<string>
        {
             OutType.Base64.ToString(),
             OutType.Hex.ToString(),
        };

        public static List<string> RSAKeyTypeValues { get; } = new List<string>
        {
        RSAKeyType.Xml.ToString(),
         RSAKeyType.PEM.ToString(),
        };
    }
}