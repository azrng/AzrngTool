using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace AzrngTools.Utils
{
    public static class JsonHelper
    {
        private static readonly JsonSerializerOptions SerializeOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };

        private static readonly JsonSerializerOptions DeserializeOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };
        /// <summary>
        /// 大文本解析改走 System.Text.Json（实测 1MB 格式化比 Newtonsoft JToken 快数倍）；
        /// 容忍注释与尾逗号，保持工具页对粘贴内容的宽容度
        /// </summary>
        private static readonly JsonDocumentOptions LenientJsonOptions = new()
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        /// <summary>
        /// Json格式化
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string JsonFormatter(string str)
        {
            using var document = JsonDocument.Parse(str, LenientJsonOptions);
            return WriteJsonDocument(document, indented: true);
        }

        /// <summary>
        /// Json压缩
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        public static string JsonCompress(string json)
        {
            using var document = JsonDocument.Parse(json, LenientJsonOptions);
            return WriteJsonDocument(document, indented: false);
        }

        public static string EscapeJsonText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var escaped = JsonConvert.ToString(text) ?? string.Empty;
            return escaped.Length >= 2 ? escaped[1..^1] : escaped;
        }

        public static string UnescapeJsonText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            if (TryParseJsonToken(text, out var token))
            {
                if (token is JValue { Type: JTokenType.String } stringToken && stringToken.Value<string>() is { } stringValue)
                {
                    try
                    {
                        return JsonFormatter(stringValue);
                    }
                    catch (System.Text.Json.JsonException)
                    {
                        return stringValue;
                    }
                }

                return token!.ToString(Formatting.Indented);
            }

            string? decoded;
            try
            {
                decoded = JsonConvert.DeserializeObject<string>(BuildJsonStringLiteral(text));
            }
            catch (Newtonsoft.Json.JsonException ex)
            {
                throw new FormatException("去除转义失败，输入不是合法的 JSON 转义文本。", ex);
            }

            if (string.IsNullOrWhiteSpace(decoded))
            {
                throw new FormatException("去除转义失败，结果为空。");
            }

            return JsonFormatter(decoded);
        }

        public static string ToJson<T>(T obj)
        {
            if (obj == null)
            {
                return string.Empty;
            }

            return System.Text.Json.JsonSerializer.Serialize(obj, SerializeOptions);
        }

        public static T? FromJson<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return default;
            }

            return System.Text.Json.JsonSerializer.Deserialize<T>(json, DeserializeOptions);
        }

        /// <summary>
        /// 用 Utf8JsonWriter 重写 JsonDocument：数字按原文回写、重复属性名保留，
        /// 保证格式化结果对输入忠实（JsonNode 会合并重复键，故不用它做格式化）
        /// </summary>
        private static string WriteJsonDocument(JsonDocument document, bool indented)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
                   {
                       Indented = indented,
                       Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                   }))
            {
                document.WriteTo(writer);
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        /// <summary>
        /// 基于 System.Text.Json 的 JSON 缩进格式化，不经过反射序列化，AOT 发布下可用
        /// </summary>
        public static string FormatJsonDocument(JsonDocument document)
        {
            return WriteJsonDocument(document, indented: true);
        }

        private static bool TryParseJsonToken(string text, out JToken? token)
        {
            try
            {
                token = JToken.Parse(text);
                return true;
            }
            catch (Newtonsoft.Json.JsonException)
            {
                token = null;
                return false;
            }
        }

        private static string BuildJsonStringLiteral(string text)
        {
            var builder = new StringBuilder(text.Length + 2);
            builder.Append('\"');

            foreach (var ch in text)
            {
                switch (ch)
                {
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    default:
                        builder.Append(ch);
                        break;
                }
            }

            builder.Append('\"');
            return builder.ToString();
        }
    }
}
