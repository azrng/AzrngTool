#nullable disable
using System.Text.Json.Serialization;

namespace AzrngTools.Models.Analysis
{
    /// <summary>
    /// 解析SQL缓存配置
    /// </summary>
    public class AnalysisSqlCacheConfig
    {
        public AnalysisSqlCacheConfig()
        {
            SelectField = new HashSet<string>();
            SqlDetailsList = new HashSet<AnalysisSqlDetailsDto>();
        }

        public AnalysisSqlCacheConfig(string tableName, string tableSample, string sql, HashSet<string> selectFields,
                                      HashSet<AnalysisSqlDetailsDto> analysisSqlDetails) : this()
        {
            TableName = tableName?.Split(' ')[0];
            TableSample = tableSample;
            SelectField = selectFields;
            SqlDetailsList = analysisSqlDetails;
            SqlWhere = sql;
        }

        /// <summary>
        /// 表名
        /// </summary>
        public string TableName { get; set; }

        /// <summary>
        /// 表别名
        /// </summary>
        public string TableSample { get; set; }

        /// <summary>
        /// sql条件
        /// </summary>
        public string SqlWhere { get; set; }

        /// <summary>
        /// 要查询的列
        /// </summary>
        public HashSet<string> SelectField { get; set; }

        /// <summary>
        /// 条件详情
        /// </summary>
        public HashSet<AnalysisSqlDetailsDto> SqlDetailsList { get; set; }
    }


    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(List<AnalysisSqlCacheConfig>))]
    internal partial class AnalysisSqlCacheConfigContext : JsonSerializerContext
    {
    }
}