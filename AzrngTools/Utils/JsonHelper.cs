using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        /// Json格式化
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string JsonFormatter(string str)
        {
            var token = JToken.Parse(str);
            return token.ToString(Formatting.Indented);
        }

        /// <summary>
        /// Json压缩
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        public static string JsonCompress(string json)
        {
            var token = JToken.Parse(json);
            return token.ToString(Formatting.None);
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
                    catch (Newtonsoft.Json.JsonException)
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
