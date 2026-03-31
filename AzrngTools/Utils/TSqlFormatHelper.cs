using TSqlFormatter;
using TSqlFormatter.Formatters;

namespace AzrngTools.Utils
{
    /// <summary>
    /// sql格式化
    /// </summary>
    public static class TSqlFormatHelper
    {
        /// <summary>
        /// Sql格式化
        /// </summary>
        /// <param name="strSql"></param>
        /// <param name="caseWrite"></param>
        /// <returns></returns>
        public static string SqlFormat(this string strSql, bool caseWrite = true)
        {
            string result;
            if (string.IsNullOrEmpty(strSql))
                return strSql;
            try
            {
                var errorsEncountered = false;
                var formaterManager = new SqlFormattingManager();
                var formater = (TSqlStandardFormatter)formaterManager.Formatter;
                formater.Options.UppercaseKeywords = caseWrite;
                result = formaterManager.Format(strSql, ref errorsEncountered);
            }
            catch (Exception)
            {
                result = strSql;
            }

            return result;
        }

        /// <summary>
        /// sql压缩
        /// </summary>
        /// <param name="strSql"></param>
        /// <returns></returns>
        public static string CompressToString(string strSql)
        {
            strSql = strSql.Replace("//", "");
            strSql = strSql.Replace('\r', ' ');
            strSql = strSql.Replace('\n', ' ');

            var charArray = strSql.ToCharArray();
            var charList = new List<char>();
            var spaceCount = 0;
            for (int i = 0, len = charArray.Length; i < len; i++)
            {
                var chr = charArray[i];
                if (chr == ' ')
                {
                    spaceCount++;
                    if (spaceCount == 1)
                    {
                        charList.Add(chr);
                    }
                }

                if (chr == ' ')
                {
                    continue;
                }

                spaceCount = 0;
                charList.Add(chr);
            }

            var result = string.Concat(charList.ToArray());
            return result;
        }
    }
}