using System.Collections.Generic;

namespace AzrngTools.Models.Database;

/// <summary>
/// Excel 导出模型 - 完整架构信息
/// </summary>
public class ModelExportProInfo
{
    public List<SchemaSheetExport> SchemaSheets { get; set; } = new();
    public List<DataTableCategorySchemaExport> DataTableCategorySheet { get; set; } = new();
}

/// <summary>
/// 架构工作表导出
/// </summary>
public class SchemaSheetExport
{
    public string SchemaName { get; set; } = string.Empty;
    public string SchemaRemark { get; set; } = string.Empty;
    public List<SchemaSheetTableExport> SchemaSheetTables { get; set; } = new();
    public List<SchemaSheetViewExport> SchemaSheetViews { get; set; } = new();
    public List<SchemaSheetProcExport> SchemaSheetProcList { get; set; } = new();
}

/// <summary>
/// 表导出信息
/// </summary>
public class SchemaSheetTableExport
{
    public string TableName { get; set; } = string.Empty;
    public string TableComment { get; set; } = string.Empty;
    public string CreateSqlStr { get; set; } = string.Empty;
    public List<SchemaSheetTableColumnExport> SchemaSheetTableColumns { get; set; } = new();
    public List<SchemaSheetTableIndexExport> SchemaSheetTableIndexList { get; set; } = new();
}

/// <summary>
/// 表列导出信息
/// </summary>
public class SchemaSheetTableColumnExport
{
    public string ColumnName { get; set; } = string.Empty;
    public string ColumnCnName { get; set; } = string.Empty;
    public string ColumnType { get; set; } = string.Empty;
    public string DefaultValue { get; set; } = string.Empty;
    public string IsPrimary { get; set; } = string.Empty;
    public string IsNotNull { get; set; } = string.Empty;
    public string IsForeignKey { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
}

/// <summary>
/// 表索引导出信息
/// </summary>
public class SchemaSheetTableIndexExport
{
    public string IndexName { get; set; } = string.Empty;
    public string IndexType { get; set; } = string.Empty;
    public string IndexColumnList { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public string IsUnique { get; set; } = string.Empty;
}

/// <summary>
/// 视图导出信息
/// </summary>
public class SchemaSheetViewExport
{
    public string ViewName { get; set; } = string.Empty;
    public string ViewCnName { get; set; } = string.Empty;
    public string ViewComment { get; set; } = string.Empty;
    public string CreateSqlStr { get; set; } = string.Empty;
}

/// <summary>
/// 存储过程导出信息
/// </summary>
public class SchemaSheetProcExport
{
    public string ProcName { get; set; } = string.Empty;
    public string ProcCnName { get; set; } = string.Empty;
    public string ProcComment { get; set; } = string.Empty;
    public string CreateSqlStr { get; set; } = string.Empty;
}

/// <summary>
/// 数据对象目录导出
/// </summary>
public class DataTableCategorySchemaExport
{
    public string SchemaName { get; set; } = string.Empty;
    public string SchemaCnName { get; set; } = string.Empty;
    public List<SchemaStructModelExport> SchemaStructModelExports { get; set; } = new();
}

/// <summary>
/// 架构结构模型导出
/// </summary>
public class SchemaStructModelExport
{
    public string StructTypeName { get; set; } = string.Empty;
    public string StructModelName { get; set; } = string.Empty;
    public string StructModelCnName { get; set; } = string.Empty;
}
