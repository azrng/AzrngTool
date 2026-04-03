using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;
using AzrngTools.Models.Database;

namespace AzrngTools.Services.Database;

/// <summary>
/// 文档导出服务 - 支持 Excel 和 Markdown 格式
/// </summary>
public class DocumentExportService
{
    /// <summary>
    /// 导出到 Excel（完整架构：表、视图、存储过程）
    /// </summary>
    public async Task<bool> ExportToExcelAsync(
        string filePath,
        string databaseName,
        List<TableModel> tables,
        Dictionary<string, List<ColumnModel>> tableColumnsMap,
        List<ViewModel>? views = null,
        List<StoredProcedureModel>? procedures = null,
        Dictionary<string, List<IndexModel>>? tableIndexesMap = null)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var workbook = new XSSFWorkbook();
                var locatorDict = new Dictionary<string, string>();

                // 构建导出模型
                var exportModel = BuildExportModel(databaseName, tables, tableColumnsMap, views, procedures, tableIndexesMap);

                // 为每个 schema 创建 sheet
                if (exportModel.SchemaSheets.Count > 0)
                {
                    foreach (var schemaSheet in exportModel.SchemaSheets.OrderBy(p => p.SchemaName))
                    {
                        BuildSchemaSheet(workbook, "数据对象目录", schemaSheet, locatorDict);
                    }
                }

                // 创建数据对象目录 sheet
                BuildTableCategorySheet(workbook, "数据对象目录", exportModel.DataTableCategorySheet, locatorDict);

                // 设置 sheet 顺序
                workbook.SetSheetOrder("数据对象目录", 0);
                workbook.SetActiveSheet(0);
                workbook.SetSelectedTab(0);

                using var ms = new MemoryStream();
                workbook.Write(ms, leaveOpen: false);
                File.WriteAllBytes(filePath, ms.ToArray());

                LoggingService.LogOperation($"Exported Excel document: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to export Excel document.", ex);
                return false;
            }
        });
    }

    /// <summary>
    /// 构建导出模型
    /// </summary>
    private ModelExportProInfo BuildExportModel(
        string databaseName,
        List<TableModel> tables,
        Dictionary<string, List<ColumnModel>> tableColumnsMap,
        List<ViewModel>? views,
        List<StoredProcedureModel>? procedures,
        Dictionary<string, List<IndexModel>>? tableIndexesMap)
    {
        var exportModel = new ModelExportProInfo();

        // 按 schema 分组表
        var schemaGroups = tables.GroupBy(t => t.Schema).ToDictionary(g => g.Key, g => g.ToList());

        // 处理每个 schema
        foreach (var schemaName in schemaGroups.Keys)
        {
            var schemaTables = schemaGroups[schemaName];
            var schemaViews = views?.Where(v => v.Schema == schemaName).ToList() ?? new List<ViewModel>();
            var schemaProcedures = procedures?.Where(p => p.Schema == schemaName).ToList() ?? new List<StoredProcedureModel>();

            var schemaSheet = new SchemaSheetExport
            {
                SchemaName = schemaName,
                SchemaRemark = schemaName
            };

            var schemaCategory = new DataTableCategorySchemaExport
            {
                SchemaName = schemaName,
                SchemaCnName = schemaName
            };

            // 处理表
            foreach (var table in schemaTables.OrderBy(t => t.Name))
            {
                var columns = tableColumnsMap.TryGetValue($"{table.Schema}.{table.Name}", out var cols)
                    ? cols
                    : tableColumnsMap.TryGetValue(table.Name, out var legacyCols) ? legacyCols : new List<ColumnModel>();

                var indexes = tableIndexesMap?.TryGetValue($"{table.Schema}.{table.Name}", out var idxs) == true
                    ? idxs
                    : tableIndexesMap?.TryGetValue(table.Name, out var legacyIdxs) == true ? legacyIdxs : new List<IndexModel>();

                var tableExport = new SchemaSheetTableExport
                {
                    TableName = table.Name,
                    TableComment = table.Comment ?? string.Empty,
                    CreateSqlStr = BuildCreateTableScript(table, columns, indexes)
                };

                foreach (var column in columns.OrderBy(c => c.OrdinalPosition))
                {
                    tableExport.SchemaSheetTableColumns.Add(new SchemaSheetTableColumnExport
                    {
                        ColumnName = column.Name,
                        ColumnCnName = string.Empty,
                        ColumnType = column.DataType,
                        DefaultValue = HandlerDefaultValue(column.DefaultValue ?? string.Empty),
                        IsPrimary = column.IsPrimaryKey ? "是" : "否",
                        IsNotNull = column.IsNullable ? "否" : "是",
                        IsForeignKey = "否",
                        Comment = column.Comment ?? string.Empty
                    });
                }

                foreach (var index in indexes)
                {
                    tableExport.SchemaSheetTableIndexList.Add(new SchemaSheetTableIndexExport
                    {
                        IndexName = index.Name,
                        IndexType = index.IndexType,
                        IndexColumnList = index.Columns,
                        Comment = string.Empty,
                        IsUnique = index.IsUnique ? "是" : "否"
                    });
                }

                schemaSheet.SchemaSheetTables.Add(tableExport);
                schemaCategory.SchemaStructModelExports.Add(new SchemaStructModelExport
                {
                    StructTypeName = "表",
                    StructModelName = table.Name,
                    StructModelCnName = table.Comment ?? string.Empty
                });
            }

            // 处理视图
            foreach (var view in schemaViews.OrderBy(v => v.Name))
            {
                schemaSheet.SchemaSheetViews.Add(new SchemaSheetViewExport
                {
                    ViewName = view.Name,
                    ViewCnName = view.Comment ?? string.Empty,
                    ViewComment = view.Comment ?? string.Empty,
                    CreateSqlStr = view.Definition
                });

                schemaCategory.SchemaStructModelExports.Add(new SchemaStructModelExport
                {
                    StructTypeName = "视图",
                    StructModelName = view.Name,
                    StructModelCnName = view.Comment ?? string.Empty
                });
            }

            // 处理存储过程
            foreach (var procedure in schemaProcedures.OrderBy(p => p.Name))
            {
                schemaSheet.SchemaSheetProcList.Add(new SchemaSheetProcExport
                {
                    ProcName = procedure.Name,
                    ProcCnName = procedure.Comment ?? string.Empty,
                    ProcComment = procedure.Comment ?? string.Empty,
                    CreateSqlStr = procedure.Definition
                });

                schemaCategory.SchemaStructModelExports.Add(new SchemaStructModelExport
                {
                    StructTypeName = "存储过程",
                    StructModelName = procedure.Name,
                    StructModelCnName = procedure.Comment ?? string.Empty
                });
            }

            exportModel.SchemaSheets.Add(schemaSheet);
            exportModel.DataTableCategorySheet.Add(schemaCategory);
        }

        return exportModel;
    }

    /// <summary>
    /// 导出到 Markdown
    /// </summary>
    public async Task<bool> ExportToMarkdownAsync(
        string filePath,
        string databaseName,
        List<TableModel> tables,
        Dictionary<string, List<ColumnModel>> tableColumnsMap,
        List<ViewModel>? views = null,
        List<StoredProcedureModel>? procedures = null,
        Dictionary<string, List<IndexModel>>? tableIndexesMap = null)
    {
        return await Task.Run(() =>
        {
            try
            {
                var builder = new System.Text.StringBuilder();
                var safeViews = views ?? [];
                var safeProcedures = procedures ?? [];
                var totalObjects = tables.Count + safeViews.Count + safeProcedures.Count;

                builder.AppendLine($"# 数据库结构文档 - {databaseName}");
                builder.AppendLine();
                builder.AppendLine($"**生成时间**：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                builder.AppendLine();
                builder.AppendLine($"**对象总数**：{totalObjects}");
                builder.AppendLine();
                builder.AppendLine($"**表数量**：{tables.Count}");
                builder.AppendLine();
                builder.AppendLine($"**视图数量**：{safeViews.Count}");
                builder.AppendLine();
                builder.AppendLine($"**存储过程数量**：{safeProcedures.Count}");
                builder.AppendLine();
                builder.AppendLine("---");
                builder.AppendLine();
                builder.AppendLine("## 目录");
                builder.AppendLine();

                if (tables.Count > 0)
                {
                    builder.AppendLine("### 表");
                    builder.AppendLine();
                    for (var index = 0; index < tables.Count; index++)
                    {
                        var table = tables[index];
                        var anchor = BuildMarkdownAnchor(table);
                        var displayName = BuildDisplayTableName(table);
                        var description = string.IsNullOrWhiteSpace(table.Comment) ? string.Empty : $" - {table.Comment}";
                        builder.AppendLine($"{index + 1}. [{displayName}](#{anchor}){description}");
                    }
                    builder.AppendLine();
                }

                if (safeViews.Count > 0)
                {
                    builder.AppendLine("### 视图");
                    builder.AppendLine();
                    for (var index = 0; index < safeViews.Count; index++)
                    {
                        var view = safeViews[index];
                        var anchor = BuildMarkdownAnchor(BuildDisplayObjectName(view.Schema, view.Name));
                        var description = string.IsNullOrWhiteSpace(view.Comment) ? string.Empty : $" - {view.Comment}";
                        builder.AppendLine($"{index + 1}. [{BuildDisplayObjectName(view.Schema, view.Name)}](#{anchor}){description}");
                    }
                    builder.AppendLine();
                }

                if (safeProcedures.Count > 0)
                {
                    builder.AppendLine("### 存储过程");
                    builder.AppendLine();
                    for (var index = 0; index < safeProcedures.Count; index++)
                    {
                        var procedure = safeProcedures[index];
                        var anchor = BuildMarkdownAnchor(BuildDisplayObjectName(procedure.Schema, procedure.Name));
                        var description = string.IsNullOrWhiteSpace(procedure.Comment) ? string.Empty : $" - {procedure.Comment}";
                        builder.AppendLine($"{index + 1}. [{BuildDisplayObjectName(procedure.Schema, procedure.Name)}](#{anchor}){description}");
                    }
                    builder.AppendLine();
                }

                builder.AppendLine("---");
                builder.AppendLine();

                foreach (var table in tables)
                {
                    var columns = GetTableColumns(table, tableColumnsMap);
                    var indexes = GetTableIndexes(table, tableIndexesMap);
                    builder.AppendLine($"## {BuildDisplayTableName(table)}");
                    builder.AppendLine();

                    if (!string.IsNullOrWhiteSpace(table.Comment))
                    {
                        builder.AppendLine($"> {table.Comment}");
                        builder.AppendLine();
                    }

                    builder.AppendLine("### 概览");
                    builder.AppendLine();
                    builder.AppendLine($"- 架构：`{EscapeMarkdown(table.Schema)}`");
                    builder.AppendLine($"- 表名：`{EscapeMarkdown(table.Name)}`");
                    builder.AppendLine($"- 字段数：`{columns.Count}`");
                    builder.AppendLine($"- 索引数：`{indexes.Count}`");
                    builder.AppendLine();

                    builder.AppendLine("### 字段");
                    builder.AppendLine();

                    builder.AppendLine("| 序号 | 字段名 | 数据类型 | 长度 | 可空 | 主键 | 自增 | 默认值 | 备注 |");
                    builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- |");

                    for (var index = 0; index < columns.Count; index++)
                    {
                        var column = columns[index];
                        builder.AppendLine(
                            $"| {index + 1} | {EscapeMarkdown(column.Name)} | {EscapeMarkdown(column.DataType)} | {column.Length?.ToString() ?? string.Empty} | {(column.IsNullable ? "是" : "否")} | {(column.IsPrimaryKey ? "是" : "否")} | {(column.IsIdentity ? "是" : "否")} | {EscapeMarkdown(column.DefaultValue ?? string.Empty)} | {EscapeMarkdown(column.Comment ?? string.Empty)} |");
                    }

                    builder.AppendLine();

                    builder.AppendLine("### 索引");
                    builder.AppendLine();
                    if (indexes.Count == 0)
                    {
                        builder.AppendLine("_暂无索引_");
                    }
                    else
                    {
                        builder.AppendLine("| 索引名 | 类型 | 唯一 | 主键 | 字段 |");
                        builder.AppendLine("| --- | --- | --- | --- | --- |");
                        foreach (var index in indexes)
                        {
                            builder.AppendLine(
                                $"| {EscapeMarkdown(index.Name)} | {EscapeMarkdown(index.IndexType)} | {(index.IsUnique ? "是" : "否")} | {(index.IsPrimaryKey ? "是" : "否")} | {EscapeMarkdown(index.Columns)} |");
                        }
                    }

                    builder.AppendLine();
                    builder.AppendLine("### DDL");
                    builder.AppendLine();
                    builder.AppendLine("```sql");
                    builder.AppendLine(BuildCreateTableScript(table, columns, indexes).Trim());
                    builder.AppendLine("```");
                    builder.AppendLine();
                    builder.AppendLine("---");
                    builder.AppendLine();
                }

                foreach (var view in safeViews)
                {
                    builder.AppendLine($"## {BuildDisplayObjectName(view.Schema, view.Name)}");
                    builder.AppendLine();

                    if (!string.IsNullOrWhiteSpace(view.Comment))
                    {
                        builder.AppendLine($"> {view.Comment}");
                        builder.AppendLine();
                    }

                    builder.AppendLine("### 定义");
                    builder.AppendLine();
                    builder.AppendLine("```sql");
                    builder.AppendLine(string.IsNullOrWhiteSpace(view.Definition) ? "-- 暂无定义" : view.Definition.Trim());
                    builder.AppendLine("```");
                    builder.AppendLine();
                    builder.AppendLine("---");
                    builder.AppendLine();
                }

                foreach (var procedure in safeProcedures)
                {
                    builder.AppendLine($"## {BuildDisplayObjectName(procedure.Schema, procedure.Name)}");
                    builder.AppendLine();

                    if (!string.IsNullOrWhiteSpace(procedure.Comment))
                    {
                        builder.AppendLine($"> {procedure.Comment}");
                        builder.AppendLine();
                    }

                    if (!string.IsNullOrWhiteSpace(procedure.Parameters))
                    {
                        builder.AppendLine($"**参数**：{EscapeMarkdown(procedure.Parameters)}");
                        builder.AppendLine();
                    }

                    builder.AppendLine("### 定义");
                    builder.AppendLine();
                    builder.AppendLine("```sql");
                    builder.AppendLine(string.IsNullOrWhiteSpace(procedure.Definition) ? "-- 暂无定义" : procedure.Definition.Trim());
                    builder.AppendLine("```");
                    builder.AppendLine();
                    builder.AppendLine("---");
                    builder.AppendLine();
                }

                File.WriteAllText(filePath, builder.ToString(), System.Text.Encoding.UTF8);
                LoggingService.LogOperation($"Exported Markdown document: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to export Markdown document.", ex);
                return false;
            }
        });
    }

    #region Excel Sheet Building Methods

    /// <summary>
    /// 渲染数据对象目录 sheet
    /// </summary>
    private void BuildTableCategorySheet(
        IWorkbook workbook,
        string tablesSheetName,
        List<DataTableCategorySchemaExport> dataTableCategoryExport,
        Dictionary<string, string> locatorDict)
    {
        var sheet = (XSSFSheet)workbook.CreateSheet(tablesSheetName);
        var nextRowIndexInSheet = 0;

        if (!dataTableCategoryExport.Any())
        {
            sheet.CreateRow(0).CreateCell(0).SetCellValue("(无数据)");
            return;
        }

        var bodyStyle = GetBodyStyle(workbook, IndexedColors.White.Index);
        var headerCellStyle = GetHeaderStyle(workbook);

        var headers = new List<string> { "业务域名", "业务域中文名", "类型", "对象名称", "对象中文名" };
        var totalColumnSize = headers.Count;
        var totalRowCount = dataTableCategoryExport.SelectMany(x => x.SchemaStructModelExports).Count();

        for (var rh = 0; rh <= totalRowCount; rh++)
        {
            var row = sheet.CreateRow(nextRowIndexInSheet + rh);
            for (var c = 0; c < totalColumnSize; ++c)
            {
                row.CreateCell(c).CellStyle = headerCellStyle;
            }
        }

        var sortSchemas = dataTableCategoryExport.OrderBy(p => p.SchemaName).ToList();
        for (var index = 0; index < sortSchemas.Count; index++)
        {
            var schema = sortSchemas[index];
            if (index == 0)
            {
                var columnRow = GetOrCreateRow(sheet, index);
                for (var hindex = 0; hindex < headers.Count; hindex++)
                {
                    GetOrCreateCell(columnRow, hindex).SetCellValue(headers[hindex]);
                }
                nextRowIndexInSheet++;
            }

            var dataRow = GetOrCreateRow(sheet, nextRowIndexInSheet);
            var schemaTotalStructCount = schema.SchemaStructModelExports.Count;

            var schemaTableStartRowIndex = schema.SchemaStructModelExports.FindIndex(p => p.StructTypeName == "表") + nextRowIndexInSheet;
            var schemaTableEndRowIndex = schema.SchemaStructModelExports.FindLastIndex(p => p.StructTypeName == "表") + nextRowIndexInSheet;
            var schemaViewStartRowIndex = schema.SchemaStructModelExports.FindIndex(p => p.StructTypeName == "视图") + nextRowIndexInSheet;
            var schemaViewEndRowIndex = schema.SchemaStructModelExports.FindLastIndex(p => p.StructTypeName == "视图") + nextRowIndexInSheet;
            var schemaProcStartRowIndex = schema.SchemaStructModelExports.FindIndex(p => p.StructTypeName == "存储过程") + nextRowIndexInSheet;
            var schemaProcEndRowIndex = schema.SchemaStructModelExports.FindLastIndex(p => p.StructTypeName == "存储过程") + nextRowIndexInSheet;

            var schemaCell = dataRow.GetCell(0);
            schemaCell.CellStyle = bodyStyle;
            schemaCell.SetCellValue(schema.SchemaName);

            var schemaCnCell = dataRow.GetCell(1);
            schemaCnCell.CellStyle = bodyStyle;
            schemaCnCell.SetCellValue(string.IsNullOrWhiteSpace(schema.SchemaCnName) ? schema.SchemaName : schema.SchemaCnName);

            var mergeSchemaEndRowIndex = nextRowIndexInSheet + schemaTotalStructCount - 1;
            if (mergeSchemaEndRowIndex > nextRowIndexInSheet)
            {
                sheet.AddMergedRegion(new CellRangeAddress(nextRowIndexInSheet, mergeSchemaEndRowIndex, 0, 0));
                sheet.AddMergedRegion(new CellRangeAddress(nextRowIndexInSheet, mergeSchemaEndRowIndex, 1, 1));

                if (schemaTableStartRowIndex >= 0 && schemaTableEndRowIndex >= 0 && schemaTableStartRowIndex != schemaTableEndRowIndex)
                    sheet.AddMergedRegion(new CellRangeAddress(schemaTableStartRowIndex, schemaTableEndRowIndex, 2, 2));
                if (schemaViewStartRowIndex >= 0 && schemaViewEndRowIndex >= 0 && schemaViewStartRowIndex != schemaViewEndRowIndex)
                    sheet.AddMergedRegion(new CellRangeAddress(schemaViewStartRowIndex, schemaViewEndRowIndex, 2, 2));
                if (schemaProcStartRowIndex >= 0 && schemaProcEndRowIndex >= 0 && schemaProcStartRowIndex != schemaProcEndRowIndex)
                    sheet.AddMergedRegion(new CellRangeAddress(schemaProcStartRowIndex, schemaProcEndRowIndex, 2, 2));
            }

            using var dataTable = SchemaStructModelExportToDataTable(schema.SchemaStructModelExports);
            for (var x = 0; x < dataTable.Rows.Count; ++x)
            {
                var row = x == 0 ? dataRow : GetOrCreateRow(sheet, nextRowIndexInSheet);

                for (var j = 2; j < headers.Count; ++j)
                {
                    var cell = GetOrCreateCell(row, j);
                    var rawStr = dataTable.Rows[x][j - 2]?.ToString() ?? string.Empty;

                    if (int.TryParse(rawStr, out var value))
                    {
                        cell.SetCellValue(value);
                    }
                    else
                    {
                        cell.SetCellValue(rawStr);
                    }

                    var tableName = dataTable.Columns[j - 2].ColumnName;
                    if (tableName == "对象名称")
                    {
                        var locatorKey = $"{schema.SchemaName}-{rawStr}";
                        if (locatorDict.TryGetValue(locatorKey, out var address))
                        {
                            CreateHyperLink(workbook, cell, address);
                        }
                    }

                    cell.CellStyle = bodyStyle;
                }

                nextRowIndexInSheet++;
            }
        }

        sheet.SetColumnWidth(0, 15 * 256);
        sheet.SetColumnWidth(1, 18 * 256);
        sheet.SetColumnWidth(2, 15 * 256);
        sheet.SetColumnWidth(3, 35 * 256);
        sheet.SetColumnWidth(4, 13 * 256);
    }

    /// <summary>
    /// 渲染每个 schema sheet
    /// </summary>
    private void BuildSchemaSheet(
        IWorkbook workbook,
        string tablesSheetName,
        SchemaSheetExport schemaSheet,
        Dictionary<string, string> locatorDict)
    {
        var sheet = (XSSFSheet)workbook.CreateSheet(schemaSheet.SchemaName);
        var bodyStyle = GetBodyStyle(workbook, IndexedColors.White.Index);
        var headerCellStyle = GetHeaderStyle(workbook);

        var nextRowIndexInSheet = 0;

        // 创建返回链接
        CreateHyperLink(workbook, sheet.CreateRow(nextRowIndexInSheet).CreateCell(1), $"'{tablesSheetName}'!A1", "返回");
        nextRowIndexInSheet += 2;

        var colsHeaders = new List<string>
        {
            "序号", "字段名", "中文名", "字段类型", "字段说明", "默认值", "主键", "外键", "非空"
        };
        var tableSqlHeader = "建表 SQL";
        var totalColumnSize = colsHeaders.Count;

        // 处理表
        foreach (var table in schemaSheet.SchemaSheetTables.OrderBy(t => t.TableName))
        {
            nextRowIndexInSheet += 2;

            var totalRowsCount = table.SchemaSheetTableColumns.Count + 3;

            for (var rh = 0; rh < totalRowsCount; rh++)
            {
                var row = sheet.CreateRow(nextRowIndexInSheet + rh);
                for (var c = 0; c < totalColumnSize; ++c)
                {
                    row.CreateCell(c).CellStyle = headerCellStyle;
                }
            }

            locatorDict.Add($"{schemaSheet.SchemaName}-{table.TableName}", $"'{schemaSheet.SchemaName}'!A{nextRowIndexInSheet + 1}");

            nextRowIndexInSheet = FillFirstRowSection(sheet, nextRowIndexInSheet, table.TableName, table.TableComment);
            nextRowIndexInSheet = FillFixColumnValueSection(schemaSheet.SchemaName, schemaSheet.SchemaRemark, sheet, nextRowIndexInSheet, table.TableComment);
            nextRowIndexInSheet = FillTableDataInfoSection(sheet, bodyStyle, nextRowIndexInSheet, colsHeaders, table);
            nextRowIndexInSheet = FillIndexSection(sheet, bodyStyle, headerCellStyle, nextRowIndexInSheet, totalColumnSize, table);
            nextRowIndexInSheet = SqlScriptSection(sheet, bodyStyle, headerCellStyle, nextRowIndexInSheet, tableSqlHeader, totalColumnSize, table.CreateSqlStr);

            sheet.SetColumnWidth(0, 10 * 256);
            sheet.SetColumnWidth(1, 19 * 256);
            sheet.SetColumnWidth(2, 28 * 256);
            sheet.SetColumnWidth(3, 20 * 256);
            sheet.SetColumnWidth(4, 20 * 256);
            sheet.SetColumnWidth(5, 35 * 256);
            sheet.SetColumnWidth(6, 18 * 256);
            sheet.SetColumnWidth(7, 10 * 256);
            sheet.SetColumnWidth(8, 10 * 256);
            sheet.SetColumnWidth(9, 10 * 256);

            nextRowIndexInSheet++;
        }

        // 处理视图
        var viewSqlHeader = "视图 SQL";
        foreach (var view in schemaSheet.SchemaSheetViews.OrderBy(v => v.ViewName))
        {
            nextRowIndexInSheet += 2;

            CreateRowAheadOfTime(sheet, headerCellStyle, nextRowIndexInSheet, totalColumnSize);

            nextRowIndexInSheet = FillViewAndProcInfo(
                schemaSheet.SchemaName, schemaSheet.SchemaRemark, locatorDict,
                sheet, bodyStyle, headerCellStyle, nextRowIndexInSheet, totalColumnSize,
                viewSqlHeader, view.ViewName, view.ViewCnName, view.CreateSqlStr, view.ViewComment);

            nextRowIndexInSheet++;
        }

        // 处理存储过程
        var procSqlHeader = "存储过程 SQL";
        foreach (var proc in schemaSheet.SchemaSheetProcList.OrderBy(p => p.ProcName))
        {
            nextRowIndexInSheet += 2;

            CreateRowAheadOfTime(sheet, headerCellStyle, nextRowIndexInSheet, totalColumnSize);

            nextRowIndexInSheet = FillViewAndProcInfo(
                schemaSheet.SchemaName, schemaSheet.SchemaRemark, locatorDict,
                sheet, bodyStyle, headerCellStyle, nextRowIndexInSheet, totalColumnSize,
                procSqlHeader, proc.ProcName, proc.ProcCnName, proc.CreateSqlStr, proc.ProcComment);

            nextRowIndexInSheet++;
        }
    }

    /// <summary>
    /// 填充表数据
    /// </summary>
    private int FillTableDataInfoSection(
        XSSFSheet sheet,
        ICellStyle bodyStyle,
        int nextRowIndexInSheet,
        List<string> colsHeaders,
        SchemaSheetTableExport table)
    {
        var headRow = GetOrCreateRow(sheet, nextRowIndexInSheet);
        for (var chindex = 0; chindex < colsHeaders.Count; chindex++)
        {
            GetOrCreateCell(headRow, chindex).SetCellValue(colsHeaders[chindex]);
        }

        nextRowIndexInSheet++;

        var sortCols = table.SchemaSheetTableColumns.OrderByDescending(p => p.IsPrimary)
            .ThenByDescending(p => p.IsForeignKey).ToList();
        using var dataTableCols = SchemaSheetTableColumnExportToDataTable(sortCols);

        for (var x = 0; x < dataTableCols.Rows.Count; ++x)
        {
            var row = GetOrCreateRow(sheet, nextRowIndexInSheet);
            for (var j = 0; j < colsHeaders.Count; ++j)
            {
                var cell = GetOrCreateCell(row, j);
                var rawStr = j == 0 ? (x + 1).ToString() : dataTableCols.Rows[x][j - 1]?.ToString() ?? string.Empty;
                cell.SetCellValue(rawStr);
                cell.CellStyle = bodyStyle;
            }

            nextRowIndexInSheet++;
        }

        return nextRowIndexInSheet;
    }

    /// <summary>
    /// 填充视图和存储过程信息
    /// </summary>
    private int FillViewAndProcInfo(
        string schemaName, string schemaCnName,
        Dictionary<string, string> locatorDict,
        XSSFSheet sheet,
        ICellStyle bodyStyle, ICellStyle headerCellStyle,
        int nextRowIndexInSheet, int totalColumnSize,
        string sqlHeader, string structName, string structCnName,
        string createSqlStr, string remark)
    {
        locatorDict.Add($"{schemaName}-{structName}", $"'{schemaName}'!A{nextRowIndexInSheet + 1}");

        nextRowIndexInSheet = FillFirstRowSection(sheet, nextRowIndexInSheet, structName, structCnName);
        nextRowIndexInSheet = FillFixColumnValueSection(schemaName, schemaCnName, sheet, nextRowIndexInSheet, remark);
        nextRowIndexInSheet = SqlScriptSection(sheet, bodyStyle, headerCellStyle, nextRowIndexInSheet, sqlHeader, totalColumnSize, createSqlStr);

        return nextRowIndexInSheet;
    }

    /// <summary>
    /// 填充第一行
    /// </summary>
    private int FillFirstRowSection(XSSFSheet sheet, int nextRowIndexInSheet, string structName, string structCnName)
    {
        var displayName = string.IsNullOrWhiteSpace(structCnName) ? structName : $"{structName}({structCnName})";
        var headerRow = sheet.GetRow(nextRowIndexInSheet);
        var cell = headerRow.GetCell(0);
        cell.SetCellValue(displayName);

        var totalColumns = 9;
        sheet.AddMergedRegion(new CellRangeAddress(nextRowIndexInSheet, nextRowIndexInSheet, 0, totalColumns - 1));
        nextRowIndexInSheet++;
        return nextRowIndexInSheet;
    }

    /// <summary>
    /// 填充固定列值
    /// </summary>
    private int FillFixColumnValueSection(
        string schemaName, string schemaCnName,
        XSSFSheet sheet, int nextRowIndexInSheet, string remark)
    {
        var secondHeaderRow = sheet.GetRow(nextRowIndexInSheet);

        secondHeaderRow.GetCell(0).SetCellValue("业务域");
        sheet.AddMergedRegion(new CellRangeAddress(nextRowIndexInSheet, nextRowIndexInSheet, 0, 1));

        secondHeaderRow.GetCell(2).SetCellValue(string.IsNullOrWhiteSpace(schemaCnName) ? schemaName : $"{schemaName}({schemaCnName})");
        sheet.AddMergedRegion(new CellRangeAddress(nextRowIndexInSheet, nextRowIndexInSheet, 2, 3));

        secondHeaderRow.GetCell(4).SetCellValue("说明");
        secondHeaderRow.GetCell(5).SetCellValue(remark ?? string.Empty);
        sheet.AddMergedRegion(new CellRangeAddress(nextRowIndexInSheet, nextRowIndexInSheet, 5, 8));

        nextRowIndexInSheet++;
        return nextRowIndexInSheet;
    }

    /// <summary>
    /// 填充索引章节
    /// </summary>
    private int FillIndexSection(
        XSSFSheet sheet,
        ICellStyle bodyStyle, ICellStyle headerCellStyle,
        int nextRowIndexInSheet, int totalColumnSize,
        SchemaSheetTableExport table)
    {
        if (table.SchemaSheetTableIndexList.Count == 0)
            return nextRowIndexInSheet;

        var indexRowsCount = table.SchemaSheetTableIndexList.Count + 1;

        for (var rh = 0; rh <= indexRowsCount; rh++)
        {
            var indexHeaderRow = sheet.CreateRow(nextRowIndexInSheet + rh);
            for (var c = 0; c < totalColumnSize; ++c)
            {
                indexHeaderRow.CreateCell(c).CellStyle = headerCellStyle;
            }
        }

        var indexHeaders = new List<string> { "索引", "索引类型", "索引名", "索引字段列表", "说明", "是否唯一" };
        var headRowIndex = sheet.GetRow(nextRowIndexInSheet) ?? sheet.CreateRow(nextRowIndexInSheet);
        var hIndex = 0;
        foreach (var head in indexHeaders)
        {
            var indexCellHeader = headRowIndex.GetCell(hIndex) ?? headRowIndex.CreateCell(hIndex);
            indexCellHeader.SetCellValue(head);
            if (head == "索引字段列表")
            {
                sheet.AddMergedRegion(new CellRangeAddress(nextRowIndexInSheet, nextRowIndexInSheet, hIndex, hIndex + 1));
                hIndex += 2;
            }
            else if (head == "说明")
            {
                sheet.AddMergedRegion(new CellRangeAddress(nextRowIndexInSheet, nextRowIndexInSheet, hIndex, hIndex + 2));
                hIndex += 3;
            }
            else
            {
                hIndex++;
            }
            indexCellHeader.CellStyle = headerCellStyle;
        }

        nextRowIndexInSheet++;

        using var dataTableIndexs = SchemaSheetTableIndexExportToDataTable(table.SchemaSheetTableIndexList);
        for (var x = 0; x < dataTableIndexs.Rows.Count; ++x)
        {
            var row = sheet.GetRow(nextRowIndexInSheet) ?? sheet.CreateRow(nextRowIndexInSheet);

            var firstCell = row.GetCell(0) ?? row.CreateCell(0);
            firstCell.SetCellValue(dataTableIndexs.Rows[x][0]?.ToString() ?? string.Empty);
            firstCell.CellStyle = bodyStyle;

            var typeCell = row.GetCell(1) ?? row.CreateCell(1);
            typeCell.SetCellValue(dataTableIndexs.Rows[x][1]?.ToString() ?? string.Empty);
            typeCell.CellStyle = bodyStyle;

            var nameCell = row.GetCell(2) ?? row.CreateCell(2);
            nameCell.SetCellValue(dataTableIndexs.Rows[x][2]?.ToString() ?? string.Empty);
            nameCell.CellStyle = bodyStyle;

            var fieldListCell = row.GetCell(3) ?? row.CreateCell(3);
            fieldListCell.SetCellValue(dataTableIndexs.Rows[x][3]?.ToString() ?? string.Empty);
            fieldListCell.CellStyle = bodyStyle;
            sheet.AddMergedRegion(new CellRangeAddress(nextRowIndexInSheet, nextRowIndexInSheet, 3, 4));

            var remarkCell = row.GetCell(5) ?? row.CreateCell(5);
            remarkCell.SetCellValue(dataTableIndexs.Rows[x][4]?.ToString() ?? string.Empty);
            remarkCell.CellStyle = bodyStyle;
            sheet.AddMergedRegion(new CellRangeAddress(nextRowIndexInSheet, nextRowIndexInSheet, 5, 7));

            var uniqueCell = row.GetCell(8) ?? row.CreateCell(8);
            uniqueCell.SetCellValue(dataTableIndexs.Rows[x][5]?.ToString() ?? string.Empty);
            uniqueCell.CellStyle = bodyStyle;

            nextRowIndexInSheet++;
        }

        return nextRowIndexInSheet;
    }

    /// <summary>
    /// SQL 脚本文
    /// </summary>
    private int SqlScriptSection(
        XSSFSheet sheet,
        ICellStyle bodyStyle, ICellStyle headerCellStyle,
        int nextRowIndexInSheet, string sqlHeader, int totalColumnSize, string sqlStr)
    {
        if (string.IsNullOrWhiteSpace(sqlStr))
        {
            return nextRowIndexInSheet;
        }

        var sqlStrRowsExtra = CalCellExtraLine(sqlStr);
        var sqlStrRowsTotal = sqlStrRowsExtra + 1;
        var sqlStrRowsStartIndex = nextRowIndexInSheet;

        for (var rh = 0; rh < sqlStrRowsTotal; rh++)
        {
            var tmpHeaderRow = sheet.CreateRow(nextRowIndexInSheet);
            tmpHeaderRow.Height = 2000;

            for (var c = 0; c < totalColumnSize; ++c)
            {
                tmpHeaderRow.CreateCell(c).CellStyle = headerCellStyle;
            }

            if (rh == 0)
            {
                tmpHeaderRow.GetCell(0).SetCellValue(sqlHeader);
            }

            nextRowIndexInSheet++;
        }

        var sqlBodyStyle = GetSqlBodyStyle(sheet.Workbook, bodyStyle);
        var max = 32767;
        var num = sqlStr.Length / max;
        for (var i = 0; i < num; i++)
        {
            var cRowIndex = sqlStrRowsStartIndex + i;
            var cell = sheet.GetRow(cRowIndex).GetCell(1);
            cell.CellStyle = sqlBodyStyle;
            cell.SetCellValue(sqlStr.Substring(i * max, max));
            sheet.AddMergedRegion(new CellRangeAddress(cRowIndex, cRowIndex, 1, 8));
        }

        var extra = sqlStr.Length % max;
        if (extra > 0)
        {
            var cRowIndex = num + sqlStrRowsStartIndex;
            var cell = sheet.GetRow(cRowIndex).GetCell(1);
            cell.CellStyle = bodyStyle;
            cell.SetCellValue(sqlStr.Substring(num * max, extra));
            sheet.AddMergedRegion(new CellRangeAddress(cRowIndex, cRowIndex, 1, 8));
        }

        if (sqlStrRowsTotal > 1)
        {
            sheet.AddMergedRegion(new CellRangeAddress(sqlStrRowsStartIndex, sqlStrRowsTotal + sqlStrRowsStartIndex - 1, 0, 0));
        }

        return nextRowIndexInSheet;
    }

    /// <summary>
    /// 估算需要额外扩展的行数
    /// </summary>
    private int CalCellExtraLine(string sqlStr)
    {
        var maxCellChars = 32767;
        var extraLines = 0;
        if (!string.IsNullOrWhiteSpace(sqlStr))
        {
            var num = sqlStr.Length / maxCellChars;
            extraLines += (num - 1);
            if ((sqlStr.Length % maxCellChars) > 0)
                extraLines++;
        }
        return extraLines;
    }

    /// <summary>
    /// 为指定 Cell 创建超链接
    /// </summary>
    private void CreateHyperLink(IWorkbook workbook, ICell cell, string address, string cellValue = "")
    {
        if (!string.IsNullOrWhiteSpace(cellValue))
            cell.SetCellValue(cellValue);
        cell.Hyperlink = new XSSFHyperlink(HyperlinkType.Document) { Address = address };
        cell.CellStyle = GetHyperlinkStyle(workbook);
    }

    /// <summary>
    /// 获取超链接单元格样式
    /// </summary>
    private static ICellStyle GetHyperlinkStyle(IWorkbook workbook)
    {
        var hLinkStyle = workbook.CreateCellStyle();
        var hLinkFont = workbook.CreateFont();
        hLinkFont.Underline = FontUnderlineType.Single;
        hLinkFont.Color = IndexedColors.Blue.Index;
        hLinkStyle.SetFont(hLinkFont);
        return hLinkStyle;
    }

    /// <summary>
    /// 获取表头单元格样式
    /// </summary>
    private static ICellStyle GetHeaderStyle(IWorkbook workbook)
    {
        var headerStyle = workbook.CreateCellStyle();
        headerStyle.FillPattern = FillPattern.SolidForeground;
        headerStyle.FillForegroundColor = IndexedColors.LightTurquoise.Index;
        headerStyle.BorderBottom = BorderStyle.Thin;
        headerStyle.BorderLeft = BorderStyle.Thin;
        headerStyle.BorderRight = BorderStyle.Thin;
        headerStyle.BorderTop = BorderStyle.Thin;
        headerStyle.Alignment = HorizontalAlignment.Center;
        headerStyle.VerticalAlignment = VerticalAlignment.Center;
        var font = workbook.CreateFont();
        font.IsBold = true;
        headerStyle.SetFont(font);
        return headerStyle;
    }

    /// <summary>
    /// 默认单元格样式
    /// </summary>
    private static ICellStyle GetBodyStyle(IWorkbook workbook, short color)
    {
        var cellDefaultStyle = workbook.CreateCellStyle();
        cellDefaultStyle.FillPattern = FillPattern.SolidForeground;
        cellDefaultStyle.FillForegroundColor = color;
        cellDefaultStyle.BorderBottom = BorderStyle.Thin;
        cellDefaultStyle.BorderLeft = BorderStyle.Thin;
        cellDefaultStyle.BorderRight = BorderStyle.Thin;
        cellDefaultStyle.BorderTop = BorderStyle.Thin;
        cellDefaultStyle.Alignment = HorizontalAlignment.Center;
        cellDefaultStyle.VerticalAlignment = VerticalAlignment.Center;
        return cellDefaultStyle;
    }

    /// <summary>
    /// SQL 单元格样式
    /// </summary>
    private static ICellStyle GetSqlBodyStyle(IWorkbook workbook, ICellStyle baseStyle)
    {
        var sqlStyle = workbook.CreateCellStyle();
        sqlStyle.CloneStyleFrom(baseStyle);
        sqlStyle.Alignment = HorizontalAlignment.Left;
        sqlStyle.VerticalAlignment = VerticalAlignment.Top;
        sqlStyle.WrapText = true;
        return sqlStyle;
    }

    /// <summary>
    /// 提前创建行
    /// </summary>
    private void CreateRowAheadOfTime(XSSFSheet sheet, ICellStyle headerCellStyle, int nextRowIndexInSheet, int totalColumnSize)
    {
        for (var rh = 0; rh < 2; rh++)
        {
            var schemaHeaderRow = sheet.CreateRow(nextRowIndexInSheet + rh);
            for (var c = 0; c < totalColumnSize; ++c)
            {
                schemaHeaderRow.CreateCell(c).CellStyle = headerCellStyle;
            }
        }
    }

    /// <summary>
    /// 处理默认值
    /// </summary>
    private static IRow GetOrCreateRow(ISheet sheet, int rowIndex)
    {
        return sheet.GetRow(rowIndex) ?? sheet.CreateRow(rowIndex);
    }

    private static ICell GetOrCreateCell(IRow row, int cellIndex)
    {
        return row.GetCell(cellIndex) ?? row.CreateCell(cellIndex);
    }

    private string HandlerDefaultValue(string defaultValue)
    {
        if (string.IsNullOrEmpty(defaultValue))
            return defaultValue;
        return defaultValue.Replace("::character varying", "").Replace("::text", "");
    }

    /// <summary>
    /// 构建表显示名称
    /// </summary>
    private static string BuildDisplayTableName(TableModel table)
    {
        return string.IsNullOrWhiteSpace(table.Schema) ? table.Name : $"{table.Schema}.{table.Name}";
    }

    private static string BuildDisplayObjectName(string? schemaName, string name)
    {
        return string.IsNullOrWhiteSpace(schemaName) ? name : $"{schemaName}.{name}";
    }

    /// <summary>
    /// 构建 Markdown 锚点
    /// </summary>
    private static string BuildMarkdownAnchor(TableModel table)
    {
        return BuildMarkdownAnchor(BuildDisplayTableName(table));
    }

    private static string BuildMarkdownAnchor(string objectName)
    {
        return objectName
            .ToLowerInvariant()
            .Replace(".", string.Empty)
            .Replace("_", string.Empty)
            .Replace(" ", "-");
    }

    /// <summary>
    /// 转义 Markdown 字符
    /// </summary>
    private static string EscapeMarkdown(string value)
    {
        return value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", "<br/>");
    }

    /// <summary>
    /// 获取表列
    /// </summary>
    private static IReadOnlyList<ColumnModel> GetTableColumns(
        TableModel table,
        IReadOnlyDictionary<string, List<ColumnModel>> tableColumnsMap)
    {
        var fullKey = $"{table.Schema}.{table.Name}";
        if (tableColumnsMap.TryGetValue(fullKey, out var fullMatch))
        {
            return fullMatch;
        }

        return tableColumnsMap.TryGetValue(table.Name, out var legacyMatch)
            ? legacyMatch
            : Array.Empty<ColumnModel>();
    }

    /// <summary>
    /// 获取表索引
    /// </summary>
    private static IReadOnlyList<IndexModel> GetTableIndexes(
        TableModel table,
        IReadOnlyDictionary<string, List<IndexModel>>? tableIndexesMap)
    {
        if (tableIndexesMap == null)
        {
            return Array.Empty<IndexModel>();
        }

        var fullKey = $"{table.Schema}.{table.Name}";
        if (tableIndexesMap.TryGetValue(fullKey, out var fullMatch))
        {
            return fullMatch;
        }

        return tableIndexesMap.TryGetValue(table.Name, out var legacyMatch)
            ? legacyMatch
            : Array.Empty<IndexModel>();
    }

    /// <summary>
    /// 构建创建表脚本
    /// </summary>
    private static string BuildCreateTableScript(TableModel table, IReadOnlyList<ColumnModel> columns, IReadOnlyList<IndexModel> indexes)
    {
        var builder = new System.Text.StringBuilder();
        var qualifiedTableName = string.IsNullOrWhiteSpace(table.Schema)
            ? table.Name
            : $"{table.Schema}.{table.Name}";

        builder.AppendLine($"CREATE TABLE {qualifiedTableName}");
        builder.AppendLine("(");

        for (var index = 0; index < columns.Count; index++)
        {
            var column = columns[index];
            var lineBuilder = new System.Text.StringBuilder();
            lineBuilder.Append("    ");
            lineBuilder.Append(column.Name);
            lineBuilder.Append(' ');
            lineBuilder.Append(column.DataType);

            if (column.Length > 0 &&
                !column.DataType.Contains("text", StringComparison.OrdinalIgnoreCase) &&
                !column.DataType.Contains("date", StringComparison.OrdinalIgnoreCase) &&
                !column.DataType.Contains("time", StringComparison.OrdinalIgnoreCase))
            {
                lineBuilder.Append('(');
                lineBuilder.Append(column.Length);
                lineBuilder.Append(')');
            }

            if (!column.IsNullable)
            {
                lineBuilder.Append(" NOT NULL");
            }

            if (!string.IsNullOrWhiteSpace(column.DefaultValue))
            {
                lineBuilder.Append(" DEFAULT ");
                lineBuilder.Append(column.DefaultValue);
            }

            if (column.IsIdentity)
            {
                lineBuilder.Append(" IDENTITY");
            }

            if (index < columns.Count - 1 || columns.Any(col => col.IsPrimaryKey))
            {
                lineBuilder.Append(',');
            }

            builder.AppendLine(lineBuilder.ToString());
        }

        var primaryKeyColumns = columns
            .Where(column => column.IsPrimaryKey)
            .OrderBy(column => column.OrdinalPosition)
            .Select(column => column.Name)
            .ToList();

        if (primaryKeyColumns.Count > 0)
        {
            builder.AppendLine($"    PRIMARY KEY ({string.Join(", ", primaryKeyColumns)})");
        }

        builder.AppendLine(");");

        if (!string.IsNullOrWhiteSpace(table.Comment))
        {
            builder.AppendLine();
            builder.AppendLine($"-- Comment: {table.Comment}");
        }

        if (indexes.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("-- Indexes");

            foreach (var index in indexes)
            {
                var uniqueText = index.IsUnique ? "UNIQUE " : string.Empty;
                builder.AppendLine($"CREATE {uniqueText}INDEX {index.Name} ON {qualifiedTableName} ({index.Columns});");
            }
        }

        return builder.ToString();
    }

    #region DataTable Conversion Helpers

    private static DataTable SchemaStructModelExportToDataTable(List<SchemaStructModelExport> exports)
    {
        var table = new DataTable();
        table.Columns.Add("类型", typeof(string));
        table.Columns.Add("对象名称", typeof(string));
        table.Columns.Add("对象中文名", typeof(string));

        foreach (var export in exports)
        {
            table.Rows.Add(export.StructTypeName, export.StructModelName, export.StructModelCnName);
        }

        return table;
    }

    private static DataTable SchemaSheetTableColumnExportToDataTable(List<SchemaSheetTableColumnExport> exports)
    {
        var table = new DataTable();
        table.Columns.Add("字段名", typeof(string));
        table.Columns.Add("中文名", typeof(string));
        table.Columns.Add("字段类型", typeof(string));
        table.Columns.Add("字段说明", typeof(string));
        table.Columns.Add("默认值", typeof(string));
        table.Columns.Add("主键", typeof(string));
        table.Columns.Add("外键", typeof(string));
        table.Columns.Add("非空", typeof(string));

        foreach (var export in exports)
        {
            table.Rows.Add(
                export.ColumnName,
                export.ColumnCnName,
                export.ColumnType,
                export.Comment,
                export.DefaultValue,
                export.IsPrimary,
                export.IsForeignKey,
                export.IsNotNull);
        }

        return table;
    }

    private static DataTable SchemaSheetTableIndexExportToDataTable(List<SchemaSheetTableIndexExport> exports)
    {
        var table = new DataTable();
        table.Columns.Add("索引", typeof(string));
        table.Columns.Add("索引类型", typeof(string));
        table.Columns.Add("索引名", typeof(string));
        table.Columns.Add("索引字段列表", typeof(string));
        table.Columns.Add("说明", typeof(string));
        table.Columns.Add("是否唯一", typeof(string));

        foreach (var export in exports)
        {
            table.Rows.Add(
                export.IndexName,
                export.IndexType,
                export.IndexName,
                export.IndexColumnList,
                export.Comment,
                export.IsUnique);
        }

        return table;
    }

    #endregion

    #endregion
}
