using System.Text.RegularExpressions;

namespace AzrngTools.Models.Analysis
{
    /// <summary>
    /// 解析SQL的详情
    /// </summary>
    public class AnalysisSqlDetailsDto
    {
        public AnalysisSqlDetailsDto()
        {
        }

        public AnalysisSqlDetailsDto(string field) : this()
        {
            Field = field;

            //非结构化
            var diffNotStuct = new string[] { "original_html_content", "original_txt_content", "original_xml_content", "original_other_content",
            "record_xml_content","record_html_content","record_txt_content","record_other_content"};
            IsNotStuct = diffNotStuct.Contains(Field);
        }

        /// <summary>
        /// 正则方案的处理
        /// </summary>
        /// <param name="sql"></param>
        public AnalysisSqlDetailsDto(string sql, bool isRegex = true) : this()
        {
            if (sql.IsNullOrEmpty())
                return;

            sql = Regex.Replace(sql, @"\s+", " ");

            //正则获取操作符
            var regularOperator = new string[] { "!~*", "!~", "~*", "~" };

            //否定操作符
            var denyOperator = new string[] { "!~", "NOT IN", "NOT LIKE", "IS NOT NULL" };

            //非结构化
            var diffNotStuct = new string[] { "original_html_content", "original_txt_content", "original_xml_content", "original_other_content",
            "record_xml_content","record_html_content","record_txt_content","record_other_content"};

            //匹配列名
            const string fieldColumnRegex = "[\\w]+\\.[\\w]+\\b";
            //匹配操作符
            const string operatorRegex = "[!~*|!~|!|~*|~]+(?=[\\s+'|'])";

            Field = Regex.Match(sql, fieldColumnRegex, RegexOptions.IgnoreCase).ToString().Split(".")[1];
            Operator = Regex.Match(sql, operatorRegex, RegexOptions.IgnoreCase).ToString();

            IsRegular = regularOperator.Contains(Operator);
            if (IsRegular)
            {
                //匹配正则值
                const string fieldValueRegex = "(?<=[!~*|!~|!|~*|~])\\s?'.+'";
                Value = Regex.Match(sql, fieldValueRegex, RegexOptions.IgnoreCase).ToString()?.Trim().Trim('\'') ?? string.Empty;
            }

            sql = sql.ToUpper();
            IsDenyOperator = denyOperator.Any(t => sql.Contains(t));
            IsNotStuct = diffNotStuct.Contains(Field);
        }

        /// <summary>
        /// 查询字段
        /// </summary>
        public string Field { get; set; }

        /// <summary>
        /// 正则操作符
        /// </summary>
        public string Operator { get; set; }

        /// <summary>
        /// 条件的值
        /// </summary>
        public string Value { get; private set; }

        /// <summary>
        /// 是否是正则
        /// </summary>
        public bool IsRegular { get; set; }

        /// <summary>
        /// 是否是否定操作符
        /// </summary>
        public bool IsDenyOperator { get; set; }

        /// <summary>
        /// 是否是非结构化
        /// </summary>
        public bool IsNotStuct { get; set; }

        /// <summary>
        /// 设置操作符
        /// </summary>
        /// <param name="operation"></param>
        /// <param name="notValue"></param>
        public void SetOperationInfo(string operation, string notValue)
        {
            Operator = notValue?.Equals("true", StringComparison.CurrentCultureIgnoreCase) == true ? $"NOT {operation}" : operation;

            //否定操作符
            var denyOperator = new string[] { "<>", "!=", "!~", "NOT IN", "NOT LIKE", "IS NOT NULL" };
            IsDenyOperator = notValue?.Equals("true", StringComparison.CurrentCultureIgnoreCase) == true || denyOperator.Any(t => t == operation);

            // 是否是正则
            var regularOperator = new string[4] { "!~*", "!~", "~*", "~" };
            IsRegular = regularOperator.Contains(Operator);

            //非结构化
            var diffNotStuct = new string[] { "original_html_content", "original_txt_content", "original_xml_content", "original_other_content",
            "record_xml_content","record_html_content","record_txt_content","record_other_content"};

            IsNotStuct = Field.Split('.').Length == 2 && diffNotStuct.Contains(Field.Split(".")[1]);
        }

        /// <summary>
        /// 设置value值
        /// </summary>
        /// <param name="value"></param>
        public void SetValue(string value)
        {
            Value = value;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(obj, null)) return false;//判断是否为null
            if (ReferenceEquals(this, obj)) return true;//判断是否为引用相等
            var other = obj as AnalysisSqlDetailsDto;
            var flag = (string.Concat(other.Field, other.Operator, other.Value) == string.Concat(this.Field, this.Operator, this.Value));
            return flag;
        }

        /// <summary>
        /// 重写返回hastcode 的方法，返回固定值1，那么就会进重写的比较方法
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return 1;
        }
    }
}