using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AzrngTools.Models.Database;

namespace AzrngTools.Services.Database;

/// <summary>
/// 代码生成服务
/// </summary>
public class CodeGenerationService
{
    /// <summary>
    /// 生成 C# 实体类
    /// </summary>
    public async Task<bool> GenerateEntityClassAsync(string outputPath, string className, List<ColumnModel> columns, string namespaceName = "SmartSQL.Entities")
    {
        return await Task.Run(() =>
        {
            try
            {
                var sb = new StringBuilder();
                var entityName = ToPascalCase(className);

                sb.AppendLine("using System;");
                sb.AppendLine("using System.ComponentModel.DataAnnotations;");
                sb.AppendLine();
                sb.AppendLine($"namespace {namespaceName};");
                sb.AppendLine();
                sb.AppendLine("/// <summary>");
                sb.AppendLine($"/// {className} 实体类");
                sb.AppendLine("/// </summary>");
                sb.AppendLine($"public class {entityName}");
                sb.AppendLine("{");

                foreach (var col in columns.OrderBy(c => c.OrdinalPosition))
                {
                    var propertyName = ToPascalCase(col.Name);
                    var propertyType = MapColumnType(col.DataType, col.IsNullable);
                    var columnName = col.Name;

                    sb.AppendLine();
                    sb.AppendLine("    /// <summary>");
                    sb.AppendLine($"    /// {col.Comment ?? propertyName}");
                    sb.AppendLine("    /// </summary>");

                    if (col.IsPrimaryKey)
                    {
                        sb.AppendLine("    [Key]");
                    }

                    sb.AppendLine($"    public {propertyType} {propertyName} {{ get; set; }}");
                }

                sb.AppendLine("}");

                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                File.WriteAllText(outputPath, sb.ToString());

                LoggingService.LogOperation($"生成实体类成功：{outputPath}");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"生成实体类失败", ex);
                return false;
            }
        });
    }

    /// <summary>
    /// 批量生成实体类
    /// </summary>
    public async Task<bool> GenerateEntityClassesAsync(string outputPath, List<TableModel> tables, Dictionary<string, List<ColumnModel>> tableColumnsMap, string namespaceName = "SmartSQL.Entities")
    {
        try
        {
            Directory.CreateDirectory(outputPath);

            var successCount = 0;
            foreach (var table in tables)
            {
                var columns = tableColumnsMap.ContainsKey(table.Name) ? tableColumnsMap[table.Name] : new List<ColumnModel>();
                var fileName = $"{ToPascalCase(table.Name)}.cs";
                var filePath = Path.Combine(outputPath, fileName);

                var result = await GenerateEntityClassAsync(filePath, table.Name, columns, namespaceName);
                if (result)
                {
                    successCount++;
                }
            }

            LoggingService.LogInfo($"批量生成实体类完成：{successCount}/{tables.Count}");
            return successCount == tables.Count;
        }
        catch (Exception ex)
        {
            LoggingService.LogError($"批量生成实体类失败", ex);
            return false;
        }
    }

    /// <summary>
    /// 生成仓储类
    /// </summary>
    public async Task<bool> GenerateRepositoryAsync(string outputPath, string className, string namespaceName = "SmartSQL.Repositories")
    {
        return await Task.Run(() =>
        {
            try
            {
                var sb = new StringBuilder();
                var repositoryName = $"{ToPascalCase(className)}Repository";
                var entityName = ToPascalCase(className);

                sb.AppendLine("using System;");
                sb.AppendLine("using System.Collections.Generic;");
                sb.AppendLine("using System.Threading.Tasks;");
                sb.AppendLine();
                sb.AppendLine($"namespace {namespaceName};");
                sb.AppendLine();
                sb.AppendLine("/// <summary>");
                sb.AppendLine($"/// {className} 仓储类");
                sb.AppendLine("/// </summary>");
                sb.AppendLine($"public class {repositoryName}");
                sb.AppendLine("{");
                sb.AppendLine($"    public async Task<{entityName}?> GetByIdAsync(int id) => await Task.FromResult<{entityName}?>(null);");
                sb.AppendLine($"    public async Task<List<{entityName}>> GetAllAsync() => await Task.FromResult(new List<{entityName}>());");
                sb.AppendLine($"    public async Task<bool> CreateAsync({entityName} entity) => await Task.FromResult(true);");
                sb.AppendLine($"    public async Task<bool> UpdateAsync({entityName} entity) => await Task.FromResult(true);");
                sb.AppendLine($"    public async Task<bool> DeleteAsync(int id) => await Task.FromResult(true);");
                sb.AppendLine("}");

                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                File.WriteAllText(outputPath, sb.ToString());

                LoggingService.LogOperation($"生成仓储类成功：{outputPath}");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"生成仓储类失败", ex);
                return false;
            }
        });
    }

    /// <summary>
    /// 生成 API Controller 类
    /// </summary>
    public async Task<bool> GenerateControllerAsync(string outputPath, string className, string namespaceName = "SmartSQL.Controllers")
    {
        return await Task.Run(() =>
        {
            try
            {
                var sb = new StringBuilder();
                var controllerName = $"{ToPascalCase(className)}Controller";
                var entityName = ToPascalCase(className);
                var repositoryName = $"{entityName}Repository";

                sb.AppendLine("using System;");
                sb.AppendLine("using System.Collections.Generic;");
                sb.AppendLine("using System.Threading.Tasks;");
                sb.AppendLine("using Microsoft.AspNetCore.Mvc;");
                sb.AppendLine();
                sb.AppendLine($"namespace {namespaceName};");
                sb.AppendLine();
                sb.AppendLine("[ApiController]");
                sb.AppendLine("[Route(\"api/[controller]\")]");
                sb.AppendLine("/// <summary>");
                sb.AppendLine($"/// {className} API 控制器");
                sb.AppendLine("/// </summary>");
                sb.AppendLine($"public class {controllerName} : ControllerBase");
                sb.AppendLine("{");
                sb.AppendLine($"    private readonly {repositoryName} _repository;");
                sb.AppendLine();
                sb.AppendLine($"    public {controllerName}({repositoryName} repository)");
                sb.AppendLine("    {");
                sb.AppendLine("        _repository = repository;");
                sb.AppendLine("    }");
                sb.AppendLine();
                sb.AppendLine("    [HttpGet]");
                sb.AppendLine($"    public async Task<ActionResult<List<{entityName}>>> GetAll() => await _repository.GetAllAsync();");
                sb.AppendLine();
                sb.AppendLine("    [HttpGet(\"{id}\")]");
                sb.AppendLine($"    public async Task<ActionResult<{entityName}>> GetById(int id) => await _repository.GetByIdAsync(id);");
                sb.AppendLine();
                sb.AppendLine("    [HttpPost]");
                sb.AppendLine($"    public async Task<IActionResult> Create({entityName} entity) => Ok(await _repository.CreateAsync(entity));");
                sb.AppendLine();
                sb.AppendLine("    [HttpPut(\"{id}\")]");
                sb.AppendLine($"    public async Task<IActionResult> Update(int id, {entityName} entity) => Ok(await _repository.UpdateAsync(entity));");
                sb.AppendLine();
                sb.AppendLine("    [HttpDelete(\"{id}\")]");
                sb.AppendLine("    public async Task<IActionResult> Delete(int id) => Ok(await _repository.DeleteAsync(id));");
                sb.AppendLine("}");

                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                File.WriteAllText(outputPath, sb.ToString());

                LoggingService.LogOperation($"生成控制器类成功：{outputPath}");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"生成控制器类失败", ex);
                return false;
            }
        });
    }

    /// <summary>
    /// 数据库类型映射到 C# 类型
    /// </summary>
    private string MapColumnType(string sqlType, bool isNullable)
    {
        var normalizedType = sqlType?.ToLowerInvariant() ?? string.Empty;

        var type = normalizedType switch
        {
            var t when t.Contains("bigint") => "long",
            var t when t.Contains("smallint") => "short",
            var t when t.Contains("tinyint") => "byte",
            var t when t.Contains("int") || t.Contains("integer") => "int",
            var t when t.Contains("bit") => "bool",
            var t when t.Contains("datetime") || t.Contains("timestamp") => "DateTime",
            var t when t.Contains("date") => "DateTime",
            var t when t.Contains("time") => "TimeSpan",
            var t when t.Contains("money") || t.Contains("decimal") => "decimal",
            var t when t.Contains("float") || t.Contains("double") || t.Contains("real") => "double",
            var t when t.Contains("char") || t.Contains("text") || t.Contains("xml") => "string",
            var t when t.Contains("binary") || t.Contains("image") => "byte[]",
            var t when t.Contains("uniqueidentifier") => "Guid",
            _ => "object"
        };

        // 值类型且可空时添加?
        if (isNullable && !type.Equals("string", StringComparison.Ordinal) && !type.Equals("byte[]", StringComparison.Ordinal))
        {
            type += "?";
        }

        return type;
    }

    /// <summary>
    /// 转换为帕斯卡命名法（首字母大写）
    /// </summary>
    private string ToPascalCase(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Unknown";
        }

        // 处理下划线命名
        var parts = name.Split('_', '-');
        var result = new StringBuilder();

        foreach (var part in parts)
        {
            if (part.Length > 0)
            {
                result.Append(char.ToUpper(part[0]));
                if (part.Length > 1)
                {
                    result.Append(part.Substring(1).ToLower());
                }
            }
        }

        // 确保首字符是字母
        var finalName = result.ToString();
        if (finalName.Length > 0 && !char.IsLetter(finalName[0]))
        {
            finalName = "Col" + finalName;
        }

        return finalName;
    }
}
