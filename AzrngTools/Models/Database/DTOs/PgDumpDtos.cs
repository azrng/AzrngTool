using System.Collections.ObjectModel;
using AzrngTools.Models.Database;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AzrngTools.Models.Database.DTOs;

/// <summary>
/// pg_dump 导出内容范围
/// </summary>
public enum PgDumpContentMode
{
    /// <summary>仅结构（--schema-only）</summary>
    SchemaOnly,

    /// <summary>仅数据（--data-only）</summary>
    DataOnly,

    /// <summary>结构 + 数据（默认）</summary>
    SchemaAndData
}

/// <summary>
/// pg_dump 输出格式
/// </summary>
public enum PgDumpOutputFormat
{
    /// <summary>纯 SQL 文本（plain，.sql，可用 psql 恢复）</summary>
    Plain,

    /// <summary>压缩二进制（custom，.dump，需 pg_restore 恢复，适合大库）</summary>
    Custom
}

/// <summary>
/// pg_dump 向导中的 schema 多选项
/// </summary>
public partial class PgDumpSchemaSelectionDto : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;

    [ObservableProperty] private string _owner = string.Empty;

    [ObservableProperty] private int _tableCount;

    [ObservableProperty] private bool _isDefault;

    [ObservableProperty] private bool _isSelected;

    [ObservableProperty] private bool _isVisible = true;

    public string DisplayLabel => IsDefault ? $"{Name}（默认）" : Name;

    public string MetaText => TableCount > 0 ? $"表 {TableCount}" : string.Empty;
}

/// <summary>
/// pg_dump 执行请求
/// </summary>
public class PgDumpRequest
{
    public required ConnectionConfig Connection { get; init; }

    public required string Database { get; init; }

    public required IReadOnlyList<string> Schemas { get; init; }

    public PgDumpContentMode Mode { get; init; } = PgDumpContentMode.SchemaAndData;

    public PgDumpOutputFormat OutputFormat { get; init; } = PgDumpOutputFormat.Plain;

    public required string PgDumpExePath { get; init; }

    public required string OutputFilePath { get; init; }
}

/// <summary>
/// pg_dump 执行结果
/// </summary>
public class PgDumpResult
{
    public bool Success { get; init; }

    public int ExitCode { get; init; }

    public string StdError { get; init; } = string.Empty;

    public string OutputPath { get; init; } = string.Empty;

    public PgDumpOutputFormat OutputFormat { get; init; } = PgDumpOutputFormat.Plain;
}

/// <summary>
/// pg_dump 向导回传给 Coordinator 的结果
/// </summary>
public class PgDumpExportResultDto
{
    public string Database { get; set; } = string.Empty;

    public PgDumpContentMode ContentMode { get; set; } = PgDumpContentMode.SchemaAndData;

    public PgDumpOutputFormat OutputFormat { get; set; } = PgDumpOutputFormat.Plain;

    public Collection<string> SelectedSchemas { get; set; } = new();

    public string PgDumpExePath { get; set; } = string.Empty;

    public string OutputFilePath { get; set; } = string.Empty;
}
