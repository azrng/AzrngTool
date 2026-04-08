using Newtonsoft.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace AzrngTools.Utils
{
    public static class JsonHelper
    {
        /// <summary>
        /// Json格式化
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string JsonFormatter(string str)
        {
            // var formatJson = JsonSerializer.Serialize(str, new JsonSerializerOptions
            //                                                         {
            //                                                             // 整齐打印
            //                                                             WriteIndented = true,
            //
            //                                                             //重新编码，解决中文乱码问题
            //                                                             Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            //                                                         });
            var jObject = JsonConvert.DeserializeObject<object>(str);
            var formatJson = JsonConvert.SerializeObject(jObject, new JsonSerializerSettings { Formatting = Formatting.Indented });
            return formatJson;
        }

        /// <summary>
        /// Json压缩
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        public static string JsonCompress(string json)
        {
            var sb = new StringBuilder();
            using (var reader = new StringReader(json))
            {
                var ch = -1;
                var lastch = -1;
                var isQuoteStart = false;
                while ((ch = reader.Read()) > -1)
                {
                    if ((char)lastch != '\\' && (char)ch == '\"')
                    {
                        isQuoteStart = !isQuoteStart;
                    }

                    if (!char.IsWhiteSpace((char)ch) || isQuoteStart)
                    {
                        sb.Append((char)ch);
                    }

                    lastch = ch;
                }
            }

            return sb.ToString();
        }

        public static string ToJson<T>(T obj)
        {
            if (obj == null)
            {
                return string.Empty;
            }

            var options = new JsonSerializerOptions
                          {
                              WriteIndented = true,
                              Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                              TypeInfoResolver = new DefaultJsonTypeInfoResolver()
                          };
            return System.Text.Json.JsonSerializer.Serialize(obj, options);
        }

        public static T? FromJson<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return default;
            }

            var options = new JsonSerializerOptions
                          {
                              Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, TypeInfoResolver = new DefaultJsonTypeInfoResolver()
                          };
            return System.Text.Json.JsonSerializer.Deserialize<T>(json, options);
        }
    }
}
