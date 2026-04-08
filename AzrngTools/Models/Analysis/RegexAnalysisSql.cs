#nullable disable
using System.Text.RegularExpressions;

namespace AzrngTools.Models.Analysis
{
    /// <summary>
    /// 解析SQL
    /// </summary>
    public class RegexAnalysisSql
    {
        public RegexAnalysisSql(string sourceSql, string selectWhereRegex)
        {
            if (string.IsNullOrWhiteSpace(sourceSql))
                throw new ArgumentNullException("原始SQL无效");
            if (string.IsNullOrWhiteSpace(selectWhereRegex))
                throw new ArgumentNullException("SQL正则配置不能为空");

            SourceSql = sourceSql + " ";
            SelectWhereRegex = selectWhereRegex;
        }

        /// <summary>
        /// 原始SQL
        /// </summary>
        public string SourceSql { get; }

        /// <summary>
        /// 查询条件正则配置
        /// </summary>
        public string SelectWhereRegex { get; set; }

        private List<WhereDetailDto> _whereSqlEnumerable;

        /// <summary>
        /// where条件集合
        /// </summary>
        public List<WhereDetailDto> WhereSqlEnumerable
        {
            get
            {
                if (_whereSqlEnumerable?.Count > 0)
                    return _whereSqlEnumerable;

                _whereSqlEnumerable = GetWhereSql(SelectWhereRegex);
                return _whereSqlEnumerable;
            }
        }

        /// <summary>
        /// 查询SQL条件集合
        /// </summary>
        /// <returns></returns>
        private List<WhereDetailDto> GetWhereSql(string selectWhereRegex)
        {
            //=, !=, <>, in, not in, ～, !～, like, not like 、 is  not null
            //匹配查询条件 以可选的(开头 + 数字字母下划线 + . + 数字字母下划线  + （或者空格 + ‘ + 任意字符 + ’ 然后限定条件）或者空格或者；号结尾
            //const string selectWhereRegex =
            //    "(?<=\\(?)[\\w]+\\.[\\w]+[!=|=|not like|like|<>|!~|!|not in|in]+[\\(\\s]*'.+?'|[\\w]+\\.[\\w]+\\sis not null(?=[)|\\s|;])";

            //   const string selectWhereRegex =
            //"[\\w]+\\.[\\w]+\\s+[not in|in|]+\\(\\s*'?.+'?,'?.+'?\\s*\\)|(?<=\\(?)[\\w]+\\.[\\w]+[!=|=|not like|like|<>|!~|!]+[\\(\\s]*'.+?'|[\\w]+\\.[\\w]+\\sis not null(?=[)|\\s|;])";
            var regexStrHandler = selectWhereRegex.Replace("\\\\\\\\", "\\\\");
            var selectWhereList = Regex.Matches(SourceSql, regexStrHandler, RegexOptions.IgnoreCase);
            if (selectWhereList.Count == 0)
            {
                return new List<WhereDetailDto>();
            }

            var whereDict = new Dictionary<int, WhereDetailDto>();

            for (var i = 0; i < selectWhereList.Count; i++)
            {
                if (i == 0)
                {
                    whereDict[i] = new WhereDetailDto(HandleBracket(selectWhereList[i].ToString()));
                    continue;
                }

                //取两个条件之间的字符串，如果中间包含括号或者or，那么就理解为是两个SQL，否则就是一个SQL
                var lastLocation = selectWhereList[i - 1].Index + selectWhereList[i - 1].Length;
                var currLocation = selectWhereList[i].Index;
                if (lastLocation > currLocation)
                {
                    //异常情况跳过
                    continue;
                }

                var intervalStr = SourceSql[lastLocation..currLocation];

                //如果间隔的字符串包含括号或者or那么就是两个SQL 否则就是一个SQL
                var isEnd = intervalStr.Contains(')') || intervalStr.Contains("or") || intervalStr.Contains("OR");
                if (!isEnd)
                {
                    var lastWhere = whereDict.LastOrDefault();
                    if (lastWhere.Value is null)
                    {
                        whereDict[i] = new WhereDetailDto(HandleBracket(selectWhereList[i].ToString()));
                    }
                    else
                    {
                        lastWhere.Value.AppendSql(HandleBracket(selectWhereList[i].ToString()));
                    }
                }
                else
                {
                    whereDict[i] = new WhereDetailDto(HandleBracket(selectWhereList[i].ToString()));
                }
            }

            return whereDict.Values.Select(t => t).ToList();
        }

        /// <summary>
        /// 获取完整的表SQL
        /// </summary>
        /// <returns></returns>
        public List<AnalysisSqlCacheConfig> GetTableDetailsInfo()
        {
            var result = new List<AnalysisSqlCacheConfig>();
            if (WhereSqlEnumerable.Count == 0)
                return result;

            //匹配查询列的正则  空格开始 + 数字字母下划线 + . + 数字字母下户线 匹配到该元素结尾
            const string matchSelectColumnRegex = "(?<=and|or)\\s+[\\w]+\\.[\\w]+\\b";

            //匹配表别名的正则
            const string tableSampleNameRegex = "[\\w]+\\.";

            //匹配表名的正则
            const string tableNameRegex = "[\\w]+.\\.[\\w]+\\s+{0}\\b";

            //key=>表别名  value：域+表名
            var tableSampleAndTableFullDict = new Dictionary<string, string>();
            foreach (var item in WhereSqlEnumerable)
            {
                //使用and进行限定一下 排除and t1.diag_code='J44.100'这个场景中J44.00被匹配上
                var querySelectList = Regex.Matches("and " + item.FullWhereSql, matchSelectColumnRegex, RegexOptions.IgnoreCase);
                if (querySelectList.Count == 0)
                    continue;

                var tableSampleStr = Regex.Match(querySelectList[0].ToString(), tableSampleNameRegex);
                if (string.IsNullOrWhiteSpace(tableSampleStr.ToString()))
                    continue;

                var tableSampleName = tableSampleStr.ToString().Split(".")[0];
                var selectFieldList = querySelectList.Select(t => t.ToString()).ToHashSet();
                if (tableSampleAndTableFullDict.ContainsKey(tableSampleName))
                {
                    result.Add(new AnalysisSqlCacheConfig(tableSampleAndTableFullDict[tableSampleName], tableSampleName,
                        item.FullWhereSql, selectFieldList, item.SqlDetailsList));
                }
                else
                {
                    var tableName = Regex.Match(SourceSql, string.Format(tableNameRegex, tableSampleName));

                    tableSampleAndTableFullDict[tableSampleName] = tableName.ToString() ?? string.Empty;
                    result.Add(new AnalysisSqlCacheConfig(tableName.ToString(), tableSampleName, item.FullWhereSql,
                        selectFieldList, item.SqlDetailsList));
                }
            }

            return result;
        }

        /// <summary>
        /// 处理后括号问题
        /// </summary>
        /// <returns></returns>
        private string HandleBracket(string match)
        {
            if (match is null)
                return string.Empty;

            //判断是否是 not in in等，这种需要添加),其他的需要去除）
            //const string existInRegex = "[\\w]+\\.[\\w]+[not in|in]+[\\(\\s]*'.+?'";  //匹配不上 in 数值的
            const string existInRegex = "[\\w]+\\.[\\w]+\\s+not in\\s+[\\(]*|[\\w]+\\.[\\w]+\\s+in\\s+[\\(]*";
            var isContainIn = Regex.IsMatch(match, existInRegex);
            return isContainIn ? (!match.EndsWith(")") ? $"{match})" : match) : match.TrimEnd().TrimEnd(')');
        }
    }

    /// <summary>
    /// 条件详情
    /// </summary>
    public class WhereDetailDto
    {
        public WhereDetailDto(string fullWhereSql)
        {
            SqlDetailsList = new HashSet<AnalysisSqlDetailsDto>();
            FullWhereSql = fullWhereSql;
            AnalysisSqlDetails(fullWhereSql);
        }

        /// <summary>
        /// 条件详情
        /// </summary>
        public HashSet<AnalysisSqlDetailsDto> SqlDetailsList { get; }

        /// <summary>
        /// 完整SQL条件
        /// </summary>
        public string FullWhereSql { get; private set; }

        /// <summary>
        /// 追加SQL
        /// </summary>
        /// <param name="whereSql"></param>
        public void AppendSql(string whereSql)
        {
            AnalysisSqlDetails(whereSql);
            FullWhereSql = FullWhereSql + " and " + whereSql;
        }

        /// <summary>
        /// 解析单个条件的详细信息
        /// </summary>
        /// <param name="singleWhere"></param>
        private void AnalysisSqlDetails(string singleWhere)
        {
            if (singleWhere.IsNullOrEmpty())
                return;

            SqlDetailsList.Add(new AnalysisSqlDetailsDto(singleWhere));
        }
    }
}