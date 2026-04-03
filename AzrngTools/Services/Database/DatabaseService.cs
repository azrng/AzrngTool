using System.Data;
using Azrng.Core.Model;
using Azrng.DataAccess;
using Azrng.DataAccess.DbBridge;
using Azrng.DataAccess.Helper;
using AzrngTools.Models.Database;

namespace AzrngTools.Services.Database
{
    /// <summary>
    /// 数据库服务
    /// </summary>
    public class DatabaseService
    {
        /// <summary>
        /// 测试数据库连接
        /// </summary>
        /// <param name="config">数据库配置</param>
        /// <returns>连接测试结果</returns>
        public async Task<(bool Success, string Message, string? Suggestion)> TestConnectionAsync(ConnectionConfig? config)
        {
            try
            {
                if (config == null)
                {
                    return (false, "数据库配置不能为空", "请检查连接配置是否正确");
                }

                // 验证必填字段
                if (string.IsNullOrWhiteSpace(config.Name))
                {
                    return (false, "连接名称不能为空", "请输入连接名称");
                }


                if (config.DatabaseType != DatabaseType.Sqlite)
                {
                    if (string.IsNullOrWhiteSpace(config.Host))
                    {
                        return (false, "服务器地址不能为空", "请输入数据库服务器地址");
                    }

                    if (config.Port <= 0)
                    {
                        return (false, "端口号无效",
                            $"请输入有效的端口号（{GetDatabaseTypeName(config.DatabaseType)} 默认端口：{GetDefaultPort(config.DatabaseType)}）");
                    }

                    var requiresUsername = !(config.DatabaseType == DatabaseType.SqlServer && config.UseWindowsAuthentication);

                    if (requiresUsername && string.IsNullOrWhiteSpace(config.Username))
                    {
                        return (false, "用户名不能为空", "请输入数据库用户名");
                    }
                }
                else
                {
                    // Sqlite 特殊验证
                    if (string.IsNullOrWhiteSpace(config.Database))
                    {
                        return (false, "Sqlite 数据库文件路径不能为空", "请选择 Sqlite 数据库文件");
                    }

                    if (!File.Exists(config.Database))
                    {
                        return (false, $"Sqlite 数据库文件不存在: {config.Database}", "请检查文件路径是否正确，或选择已存在的数据库文件");
                    }
                }

                var dbType = MapDatabaseType(config.DatabaseType);

                if (dbType == DatabaseType.Sqlite)
                {
                    var sqliteHelper = CreateSqliteDbHelper(config);
                    var success = await sqliteHelper.ConnectionTestAsync();
                    if (success)
                    {
                        LoggingService.LogOperation($"测试 Sqlite 连接成功: {config.Name}");
                        return (true, "连接成功", null);
                    }
                    else
                    {
                        return (false, "Sqlite 连接测试失败", "请检查数据库文件是否有效且未被占用");
                    }
                }

                if (config.DatabaseType == DatabaseType.SqlServer && config.UseWindowsAuthentication)
                {
                    await using var sqlConnection = new Microsoft.Data.SqlClient.SqlConnection(BuildConnectionString(config));
                    await sqlConnection.OpenAsync();
                    await using var command = new Microsoft.Data.SqlClient.SqlCommand("SELECT 1", sqlConnection);
                    await command.ExecuteScalarAsync();

                    LoggingService.LogOperation($"测试 SQL Server Windows 身份认证连接成功: {config.Name}");
                    return (true, "连接成功", null);
                }

                var dbBridge = CreateDbBridge(dbType, config);

                // 调用获取 Schema 列表来测试连接
                _ = await dbBridge.GetSchemaListAsync();

                LoggingService.LogOperation($"测试数据库连接成功: {config.Name} ({config.DatabaseType})");
                return (true, "连接成功", null);
            }
            catch (NotSupportedException ex)
            {
                LoggingService.LogError($"不支持的数据库类型: {config?.DatabaseType}", ex);
                return (false, $"不支持的数据库类型: {config?.DatabaseType}", "当前版本暂不支持此数据库类型");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"连接测试失败: {config?.Name}", ex);
                return (false, $"连接测试失败: {ex.Message}", GetConnectionErrorSuggestion(ex.Message));
            }
        }

        /// <summary>
        /// 根据错误消息提供解决建议
        /// </summary>
        private string? GetConnectionErrorSuggestion(string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                return null;
            }

            var lowerMessage = errorMessage.ToLower();

            // 网络相关错误
            if (lowerMessage.Contains("timeout") || lowerMessage.Contains(" timed out"))
            {
                return "连接超时，请检查：\n1. 网络连接是否正常\n2. 服务器地址和端口是否正确\n3. 防火墙是否允许连接";
            }

            if (lowerMessage.Contains("connection refused") || lowerMessage.Contains("无法连接"))
            {
                return "连接被拒绝，请检查：\n1. 数据库服务是否启动\n2. 服务器地址和端口是否正确\n3. 防火墙设置";
            }

            // 认证相关错误
            if (lowerMessage.Contains("login failed") ||
                lowerMessage.Contains("authentication") ||
                lowerMessage.Contains("access denied") ||
                lowerMessage.Contains("密码"))
            {
                return "身份验证失败，请检查：\n1. 用户名和密码是否正确\n2. 用户是否有访问该数据库的权限";
            }

            // 数据库不存在
            if (lowerMessage.Contains("database") && lowerMessage.Contains("not exist") ||
                lowerMessage.Contains("unknown database"))
            {
                return "数据库不存在，请检查：\n1. 数据库名称是否正确\n2. 是否需要先创建该数据库";
            }

            // SQL Server 特定错误
            if (lowerMessage.Contains("tcp provider") || lowerMessage.Contains("named pipes"))
            {
                return "SQL Server 连接错误，建议：\n1. 检查 SQL Server 是否允许远程连接\n2. 检查 SQL Server 服务是否启动\n3. 尝试使用 IP 地址代替服务器名";
            }

            // MySql 特定错误
            if (lowerMessage.Contains("MySql") && lowerMessage.Contains("host"))
            {
                return "MySql 连接错误，建议：\n1. 检查 MySql 服务是否启动\n2. 检查用户是否允许从当前 IP 连接\n3. 检查 bind-address 设置";
            }

            // 默认建议
            return "请检查：\n1. 连接参数是否正确\n2. 数据库服务是否运行\n3. 网络连接是否正常\n4. 查看日志获取详细错误信息";
        }

        /// <summary>
        /// 获取数据库类型显示名称
        /// </summary>
        private string GetDatabaseTypeName(DatabaseType dbType)
        {
            return dbType switch
            {
                DatabaseType.SqlServer => "SQL Server",
                DatabaseType.MySql => "MySql",
                DatabaseType.PostgresSql => "PostgresSql",
                DatabaseType.Oracle => "Oracle",
                DatabaseType.Sqlite => "Sqlite",
                DatabaseType.Dm => "达梦",
                _ => "数据库"
            };
        }

        /// <summary>
        /// 获取默认端口
        /// </summary>
        private int GetDefaultPort(DatabaseType dbType)
        {
            return dbType switch
            {
                DatabaseType.SqlServer => 1433,
                DatabaseType.MySql => 3306,
                DatabaseType.PostgresSql => 5432,
                DatabaseType.Oracle => 1521,
                DatabaseType.Dm => 5236,
                _ => 0
            };
        }

        /// <summary>
        /// 构建连接字符串
        /// </summary>
        /// <param name="config">数据库配置</param>
        /// <returns>连接字符串</returns>
        private string BuildConnectionString(ConnectionConfig config)
        {
            return config.DatabaseType switch
            {
                DatabaseType.SqlServer => config.UseWindowsAuthentication
                    ? $"Server={config.Host},{config.Port};Database={config.Database};Integrated Security=true;TrustServerCertificate=true;"
                    : $"Server={config.Host},{config.Port};Database={config.Database};User Id={config.Username};Password={config.Password};TrustServerCertificate=true;",
                DatabaseType.MySql =>
                    $"Server={config.Host};Port={config.Port};Database={config.Database};User Id={config.Username};Password={config.Password};",
                DatabaseType.PostgresSql =>
                    $"Host={config.Host};Port={config.Port};Database={config.Database};Username={config.Username};Password={config.Password};",
                DatabaseType.Oracle =>
                    $"Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={config.Host})(PORT={config.Port}))(CONNECT_DATA=(SERVICE_NAME={config.Database})));User Id={config.Username};Password={config.Password};",
                DatabaseType.Sqlite => $"Data Source={config.Database};",
                DatabaseType.Dm =>
                    $"Server={config.Host}:{config.Port};DATABASE={config.Database};UID={config.Username};PWD={config.Password};",
                _ => throw new NotSupportedException($"不支持的数据库类型: {config.DatabaseType}")
            };
        }

        /// <summary>
        /// 创建数据库桥接器实例
        /// </summary>
        private IBasicDbBridge CreateDbBridge(DatabaseType dbType, ConnectionConfig config)
        {
            var dataSourceConfig = CreateDataSourceConfig(dbType, config, config.Database);
            var connectionString = BuildConnectionString(config);

            return dbType switch
            {
                DatabaseType.MySql => new MySqlBasicDbBridge(dataSourceConfig),
                DatabaseType.SqlServer => new SqlServerBasicDbBridge(dataSourceConfig),
                DatabaseType.PostgresSql => new PostgreBasicDbBridge(connectionString),
                DatabaseType.Oracle => new OracleBasicDbBridge(dataSourceConfig),
                DatabaseType.ClickHouse => new ClickHouseBasicDbBridge(dataSourceConfig),
                DatabaseType.Sqlite => new SqliteBasicDbBridge(connectionString),
                _ => throw new NotSupportedException($"不支持的数据库类型: {dbType}")
            };
        }

        private IDbHelper CreateDbHelper(DatabaseType dbType, ConnectionConfig config, string databaseName)
        {
            var dataSourceConfig = CreateDataSourceConfig(dbType, config, databaseName);

            return dbType switch
            {
                DatabaseType.MySql => new MySqlDbHelper(dataSourceConfig),
                DatabaseType.SqlServer => new SqlServerDbHelper(dataSourceConfig),
                DatabaseType.PostgresSql => new PostgresSqlDbHelper(dataSourceConfig),
                DatabaseType.Oracle => new OracleDbHelper(dataSourceConfig),
                DatabaseType.Sqlite => new SqliteDbHelper(dataSourceConfig),
                _ => throw new NotSupportedException($"当前数据库类型暂不支持加载数据库列表: {dbType}")
            };
        }

        private SqliteDbHelper CreateSqliteDbHelper(ConnectionConfig config)
        {
            return new SqliteDbHelper(BuildConnectionString(config));
        }

        private DataSourceConfig CreateDataSourceConfig(DatabaseType dbType, ConnectionConfig config, string databaseName)
        {
            return new DataSourceConfig
                   {
                       Type = dbType,
                       Host = config.Host,
                       Port = config.Port,
                       DbName = databaseName,
                       User = config.Username,
                       UserId = config.Username,
                       Password = config.Password
                   };
        }

        private DatabaseType MapDatabaseType(DatabaseType type)
        {
            return type switch
            {
                DatabaseType.SqlServer => DatabaseType.SqlServer,
                DatabaseType.MySql => DatabaseType.MySql,
                DatabaseType.PostgresSql => DatabaseType.PostgresSql,
                DatabaseType.Oracle => DatabaseType.Oracle,
                DatabaseType.Sqlite => DatabaseType.Sqlite,
                DatabaseType.Dm => DatabaseType.SqlServer,
                _ => DatabaseType.SqlServer
            };
        }

        private string GetCatalogDatabaseName(DatabaseType dbType, string currentDatabaseName)
        {
            return dbType switch
            {
                DatabaseType.SqlServer => "master",
                DatabaseType.MySql => string.IsNullOrWhiteSpace(currentDatabaseName) ? "information_schema" : currentDatabaseName,
                DatabaseType.PostgresSql => string.IsNullOrWhiteSpace(currentDatabaseName) ? "postgres" : currentDatabaseName,
                DatabaseType.Oracle => currentDatabaseName,
                _ => currentDatabaseName
            };
        }

        /// <summary>
        /// 获取数据库名称列表
        /// </summary>
        public async Task<(bool Success, List<string> Databases, string Message)> GetDatabaseNamesAsync(ConnectionConfig config)
        {
            try
            {
                if (config == null)
                {
                    return (false, new List<string>(), "数据库配置不能为空");
                }

                if (config.DatabaseType == DatabaseType.SqlServer && config.UseWindowsAuthentication)
                {
                    var windowsAuthDatabases = new List<string>();

                    await using var sqlConnection = new Microsoft.Data.SqlClient.SqlConnection(BuildConnectionString(config));
                    await sqlConnection.OpenAsync();
                    await using var command =
                        new Microsoft.Data.SqlClient.SqlCommand("SELECT [name] FROM sys.databases ORDER BY [name]", sqlConnection);
                    await using var reader = await command.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        if (!reader.IsDBNull(0))
                        {
                            windowsAuthDatabases.Add(reader.GetString(0));
                        }
                    }

                    return (true, windowsAuthDatabases, $"Loaded {windowsAuthDatabases.Count} databases successfully.");
                }

                var dbType = MapDatabaseType(config.DatabaseType);
                var dbBridge = CreateDbBridge(dbType, config);
                var databases = await dbBridge.GetDatabaseNameListAsync();
                var normalizedDatabases = databases
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return (true, normalizedDatabases, $"成功加载 {normalizedDatabases.Count} 个数据库");
            }
            catch (NotSupportedException ex)
            {
                return (false, new List<string>(), ex.Message);
            }
            catch (Exception ex)
            {
                return (false, new List<string>(), $"加载数据库列表失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取数据库 Schema 列表
        /// </summary>
        /// <param name="config">数据库配置</param>
        /// <returns>Schema 列表</returns>
        public async Task<(bool Success, List<SchemaModel> Schemas, string Message)> GetSchemasAsync(ConnectionConfig config)
        {
            try
            {
                if (config == null)
                {
                    return (false, new List<SchemaModel>(), "数据库配置不能为空");
                }

                var dbType = MapDatabaseType(config.DatabaseType);

                if (dbType == DatabaseType.Sqlite)
                {
                    var sqliteSchemas = new List<SchemaModel>
                                        {
                                            new() { Name = "main", Owner = "Sqlite", TableCount = 0, IsDefault = true }
                                        };

                    return (true, sqliteSchemas, "成功加载 1 个 Schema");
                }

                var dbBridge = CreateDbBridge(dbType, config);

                var schemaList = await dbBridge.GetSchemaListAsync();

                var schemas = schemaList.Select(dto => new SchemaModel
                                                       {
                                                           Name = dto.SchemaName,
                                                           Owner = dto.SchemaComment ?? string.Empty,
                                                           TableCount = 0,
                                                           IsDefault = dto.SchemaName.Equals("dbo", StringComparison.OrdinalIgnoreCase) ||
                                                                       dto.SchemaName.Equals("public",
                                                                           StringComparison.OrdinalIgnoreCase) ||
                                                                       dto.SchemaName.Equals(config.Username,
                                                                           StringComparison.OrdinalIgnoreCase)
                                                       })
                                        .ToList();

                return (true, schemas, $"成功加载 {schemas.Count} 个 Schema");
            }
            catch (NotSupportedException ex)
            {
                return (false, new List<SchemaModel>(), $"不支持的数据库类型: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, new List<SchemaModel>(), $"加载 Schema 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取指定 Schema 下的表列表
        /// </summary>
        /// <param name="config">数据库配置</param>
        /// <param name="schemaName">Schema 名称</param>
        /// <returns>表列表</returns>
        public async Task<(bool Success, List<TableModel> Tables, string Message)> GetTablesAsync(
            ConnectionConfig config, string schemaName)
        {
            try
            {
                if (config == null)
                {
                    return (false, new List<TableModel>(), "数据库配置不能为空");
                }

                if (string.IsNullOrWhiteSpace(schemaName))
                {
                    return (false, new List<TableModel>(), "Schema 名称不能为空");
                }

                var dbType = MapDatabaseType(config.DatabaseType);

                var dbBridge = CreateDbBridge(dbType, config);

                var tableList = await dbBridge.GetTableInfoListAsync(schemaName);

                var tables = tableList.Select(dto => new TableModel
                                                     {
                                                         Name = dto.TableName,
                                                         Schema = schemaName,
                                                         TableType = "TABLE",
                                                         Comment = dto.TableComment ?? string.Empty
                                                     })
                                      .ToList();

                return (true, tables, $"成功加载 {tables.Count} 个表");
            }
            catch (NotSupportedException ex)
            {
                return (false, new List<TableModel>(), $"不支持的数据库类型: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, new List<TableModel>(), $"加载表失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取指定表的列信息
        /// </summary>
        /// <param name="config">数据库配置</param>
        /// <param name="schemaName">Schema 名称</param>
        /// <param name="tableName">表名</param>
        /// <returns>列列表</returns>
        public async Task<(bool Success, List<ColumnModel> Columns, string Message)> GetColumnsAsync(
            ConnectionConfig config, string schemaName, string tableName)
        {
            try
            {
                if (config == null)
                {
                    return (false, new List<ColumnModel>(), "数据库配置不能为空");
                }

                if (string.IsNullOrWhiteSpace(schemaName))
                {
                    return (false, new List<ColumnModel>(), "Schema 名称不能为空");
                }

                if (string.IsNullOrWhiteSpace(tableName))
                {
                    return (false, new List<ColumnModel>(), "表名不能为空");
                }

                var dbType = MapDatabaseType(config.DatabaseType);

                var dbBridge = CreateDbBridge(dbType, config);

                var columnList = await dbBridge.GetColumnListAsync(schemaName, tableName);

                var columns = columnList.Select(dto => new ColumnModel
                                                       {
                                                           Name = dto.ColumnName,
                                                           DataType = dto.ColumnType,
                                                           Length = int.TryParse(dto.ColumnLength, out var len) ? len : null,
                                                           IsNullable = dto.IsNull,
                                                           IsPrimaryKey = dto.IsPrimaryKey,
                                                           IsIdentity = dto.IsIdentity,
                                                           DefaultValue = dto.ColumnDefault ?? string.Empty,
                                                           Comment = dto.ColumnComment ?? string.Empty,
                                                           OrdinalPosition = dto.RowNumber
                                                       })
                                        .OrderBy(c => c.OrdinalPosition)
                                        .ToList();

                return (true, columns, $"成功加载 {columns.Count} 个列");
            }
            catch (NotSupportedException ex)
            {
                return (false, new List<ColumnModel>(), $"不支持的数据库类型: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, new List<ColumnModel>(), $"加载列失败: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> UpdateTableCommentAsync(
            ConnectionConfig config,
            string schemaName,
            string tableName,
            string? comment)
        {
            try
            {
                if (config == null)
                {
                    return (false, "数据库配置不能为空");
                }

                if (string.IsNullOrWhiteSpace(tableName))
                {
                    return (false, "表名不能为空");
                }

                var dbType = MapDatabaseType(config.DatabaseType);
                if (dbType is not (DatabaseType.PostgresSql or DatabaseType.MySql))
                {
                    return (false, "当前连接类型暂不支持修改备注");
                }

                var normalizedComment = NormalizeComment(comment);
                var qualifiedTableName = BuildQualifiedTableName(dbType, schemaName, tableName);
                var dbHelper = CreateDbHelper(dbType, config, config.Database);

                var sql = dbType switch
                {
                    DatabaseType.PostgresSql => string.IsNullOrEmpty(normalizedComment)
                        ? $"COMMENT ON TABLE {qualifiedTableName} IS NULL;"
                        : $"COMMENT ON TABLE {qualifiedTableName} IS '{EscapeSqlLiteral(normalizedComment)}';",
                    DatabaseType.MySql => $"ALTER TABLE {qualifiedTableName} COMMENT = '{EscapeSqlLiteral(normalizedComment)}';",
                    _ => throw new NotSupportedException("当前连接类型暂不支持修改备注")
                };

                await dbHelper.ExecuteAsync(sql);
                return (true, "表备注已更新");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"更新表备注失败：{schemaName}.{tableName}", ex);
                return (false, $"更新表备注失败：{ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> UpdateColumnCommentAsync(
            ConnectionConfig config,
            string schemaName,
            string tableName,
            string columnName,
            string? comment)
        {
            try
            {
                if (config == null)
                {
                    return (false, "数据库配置不能为空");
                }

                if (string.IsNullOrWhiteSpace(tableName))
                {
                    return (false, "表名不能为空");
                }

                if (string.IsNullOrWhiteSpace(columnName))
                {
                    return (false, "字段名不能为空");
                }

                var dbType = MapDatabaseType(config.DatabaseType);
                if (dbType is not (DatabaseType.PostgresSql or DatabaseType.MySql))
                {
                    return (false, "当前连接类型暂不支持修改备注");
                }

                var normalizedComment = NormalizeComment(comment);
                var dbHelper = CreateDbHelper(dbType, config, config.Database);

                if (dbType == DatabaseType.PostgresSql)
                {
                    var qualifiedColumnName = BuildQualifiedColumnName(dbType, schemaName, tableName, columnName);
                    var sql = string.IsNullOrEmpty(normalizedComment)
                        ? $"COMMENT ON COLUMN {qualifiedColumnName} IS NULL;"
                        : $"COMMENT ON COLUMN {qualifiedColumnName} IS '{EscapeSqlLiteral(normalizedComment)}';";

                    await dbHelper.ExecuteAsync(sql);
                    return (true, "字段备注已更新");
                }

                var columnDefinition = await LoadMySqlColumnDefinitionAsync(dbHelper, schemaName, tableName, columnName);
                if (columnDefinition == null)
                {
                    return (false, "无法获取字段当前定义，已取消保存");
                }

                if (IsMySqlGeneratedColumn(columnDefinition))
                {
                    return (false, "暂不支持修改生成列备注");
                }

                var modifySql = BuildMySqlModifyColumnCommentSql(schemaName, tableName, columnDefinition, normalizedComment);
                await dbHelper.ExecuteAsync(modifySql);
                return (true, "字段备注已更新");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"更新字段备注失败：{schemaName}.{tableName}.{columnName}", ex);
                return (false, $"更新字段备注失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 获取指定 Schema 下的视图列表
        /// </summary>
        /// <param name="config">数据库配置</param>
        /// <param name="schemaName">Schema 名称</param>
        /// <returns>视图列表</returns>
        public async Task<(bool Success, List<ViewModel> Views, string Message)> GetViewsAsync(ConnectionConfig config, string schemaName)
        {
            try
            {
                if (config == null)
                {
                    return (false, new List<ViewModel>(), "数据库配置不能为空");
                }

                if (string.IsNullOrWhiteSpace(schemaName))
                {
                    return (false, new List<ViewModel>(), "Schema 名称不能为空");
                }

                var dbType = MapDatabaseType(config.DatabaseType);

                var dbBridge = CreateDbBridge(dbType, config);

                var viewList = await dbBridge.GetSchemaViewListAsync(schemaName);

                var views = viewList.Select(dto => new ViewModel
                                                   {
                                                       Name = dto.ViewName,
                                                       Schema = dto.ViewOwner,
                                                       Definition = dto.ViewDefinition ?? string.Empty,
                                                       Comment = dto.ViewDescription ?? string.Empty
                                                   })
                                    .OrderBy(v => v.Name)
                                    .ToList();

                return (true, views, $"成功加载 {views.Count} 个视图");
            }
            catch (NotSupportedException ex)
            {
                return (false, new List<ViewModel>(), $"不支持的数据库类型: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, new List<ViewModel>(), $"加载视图失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取指定 Schema 下的存储过程列表
        /// </summary>
        /// <param name="config">数据库配置</param>
        /// <param name="schemaName">Schema 名称</param>
        /// <returns>存储过程列表</returns>
        public async Task<(bool Success, List<StoredProcedureModel> Procedures, string Message)> GetStoredProceduresAsync(
            ConnectionConfig config, string schemaName)
        {
            try
            {
                if (config == null)
                {
                    return (false, new List<StoredProcedureModel>(), "数据库配置不能为空");
                }

                if (string.IsNullOrWhiteSpace(schemaName))
                {
                    return (false, new List<StoredProcedureModel>(), "Schema 名称不能为空");
                }

                var dbType = MapDatabaseType(config.DatabaseType);

                if (dbType == DatabaseType.Sqlite)
                {
                    return (true, new List<StoredProcedureModel>(), "Sqlite 不支持存储过程");
                }

                var dbBridge = CreateDbBridge(dbType, config);
                var procedures = new List<StoredProcedureModel>();

                if (dbType == DatabaseType.MySql)
                {
                    var routineList = await dbBridge.GetSchemaRoutineListAsync(schemaName);
                    procedures = routineList
                        .Where(dto => string.Equals(dto.RoutineType, "PROCEDURE", StringComparison.OrdinalIgnoreCase))
                        .Select(dto => new StoredProcedureModel
                        {
                            Name = dto.RoutineName,
                            Schema = dto.SchemaName,
                            Definition = dto.RoutineDefinition ?? string.Empty,
                            Parameters = $"{dto.InputParam ?? ""} {dto.OutputParam ?? ""}".Trim(),
                            Comment = dto.RoutineDescription ?? string.Empty,
                            RoutineType = dto.RoutineType ?? "PROCEDURE"
                        })
                        .OrderBy(p => p.Name)
                        .ToList();
                }
                else
                {
                    var procList = await dbBridge.GetSchemaProcListAsync(schemaName);
                    procedures = procList.Select(dto => new StoredProcedureModel
                                            {
                                                Name = dto.ProcName,
                                                Schema = schemaName,
                                                Definition = dto.ProcDefinition ?? string.Empty,
                                                Parameters = $"{dto.InputParam ?? ""} {dto.OutputParam ?? ""}".Trim(),
                                                Comment = dto.ProcDescription ?? string.Empty
                                            })
                                         .OrderBy(p => p.Name)
                                         .ToList();
                }

                return (true, procedures, $"成功加载 {procedures.Count} 个存储过程");
            }
            catch (NotSupportedException ex)
            {
                return (false, new List<StoredProcedureModel>(), $"不支持的数据库类型: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, new List<StoredProcedureModel>(), $"加载存储过程失败: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Definition, string Message)> GetViewDefinitionAsync(
            ConnectionConfig config,
            string schemaName,
            string viewName)
        {
            try
            {
                if (config == null)
                {
                    return (false, string.Empty, "数据库配置不能为空");
                }

                if (string.IsNullOrWhiteSpace(schemaName) || string.IsNullOrWhiteSpace(viewName))
                {
                    return (false, string.Empty, "视图信息不能为空");
                }

                var dbType = MapDatabaseType(config.DatabaseType);

                var dbBridge = CreateDbBridge(dbType, config);
                var view = await dbBridge.GetSchemaViewAsync(schemaName, viewName);
                var ddl = view?.ViewDefinition;
                return string.IsNullOrWhiteSpace(ddl)
                    ? (false, string.Empty, "未查询到视图定义")
                    : (true, ddl!, "成功加载视图定义");
            }
            catch (Exception ex)
            {
                return (false, string.Empty, $"加载视图定义失败: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Definition, string Message)> GetStoredProcedureDefinitionAsync(
            ConnectionConfig config,
            string schemaName,
            string procedureName)
        {
            try
            {
                if (config == null)
                {
                    return (false, string.Empty, "数据库配置不能为空");
                }

                if (string.IsNullOrWhiteSpace(schemaName) || string.IsNullOrWhiteSpace(procedureName))
                {
                    return (false, string.Empty, "存储过程信息不能为空");
                }

                var dbType = MapDatabaseType(config.DatabaseType);
                if (dbType == DatabaseType.Sqlite)
                {
                    return (false, string.Empty, "Sqlite 不支持存储过程");
                }

                var dbBridge = CreateDbBridge(dbType, config);
                var routine = await dbBridge.GetSchemaRoutineAsync(schemaName, procedureName);
                var ddl = routine?.RoutineDefinition;
                return string.IsNullOrWhiteSpace(ddl)
                    ? (false, string.Empty, "未查询到存储过程定义")
                    : (true, ddl!, "成功加载存储过程定义");
            }
            catch (Exception ex)
            {
                return (false, string.Empty, $"加载存储过程定义失败: {ex.Message}");
            }
        }

        public async Task<(bool Success, List<IndexModel> Indexes, string Message)> GetIndexesAsync(
            ConnectionConfig config,
            string schemaName,
            string tableName)
        {
            try
            {
                if (config == null)
                {
                    return (false, new List<IndexModel>(), "Database configuration cannot be null.");
                }

                if (string.IsNullOrWhiteSpace(schemaName) || string.IsNullOrWhiteSpace(tableName))
                {
                    return (false, new List<IndexModel>(), "Schema name and table name are required.");
                }

                var dbType = MapDatabaseType(config.DatabaseType);
                var dbBridge = CreateDbBridge(dbType, config);
                var rawIndexes = await dbBridge.GetIndexListAsync(schemaName, tableName);

                var indexes = rawIndexes
                              .GroupBy(index => index.IndexName)
                              .Select(group =>
                              {
                                  var orderedRows = group.OrderBy(index => index.IndexPostion).ToList();
                                  var first = orderedRows[0];
                                  return new IndexModel
                                         {
                                             Name = first.IndexName ?? string.Empty,
                                             IndexType =
                                                 first.Indisprimary ? "PRIMARY KEY" : (first.Indisunique ? "UNIQUE INDEX" : "INDEX"),
                                             IsUnique = first.Indisunique,
                                             IsPrimaryKey = first.Indisprimary,
                                             Columns = string.Join(", ",
                                                 orderedRows.Select(index => index.ColumnName)
                                                            .Where(column => !string.IsNullOrWhiteSpace(column))),
                                             IsAscending = !orderedRows.Any(index =>
                                                 string.Equals(index.IndexSort, "DESC", StringComparison.OrdinalIgnoreCase))
                                         };
                              })
                              .OrderBy(index => index.IsPrimaryKey ? 0 : 1)
                              .ThenBy(index => index.Name)
                              .ToList();

                return (true, indexes, $"Loaded {indexes.Count} indexes.");
            }
            catch (NotSupportedException ex)
            {
                return (false, new List<IndexModel>(), ex.Message);
            }
            catch (Exception ex)
            {
                return (false, new List<IndexModel>(), $"Failed to load indexes: {ex.Message}");
            }
        }

        public async Task<(bool Success, bool HasResultSet, List<string> Columns, List<List<string>> Rows, int AffectedRows, string Message
            )> ExecuteSqlAsync(
            ConnectionConfig config,
            string sql)
        {
            try
            {
                if (config == null)
                {
                    return (false, false, new List<string>(), new List<List<string>>(), 0, "Database configuration cannot be null.");
                }

                if (string.IsNullOrWhiteSpace(sql))
                {
                    return (false, false, new List<string>(), new List<List<string>>(), 0, "SQL text cannot be empty.");
                }

                var dbType = MapDatabaseType(config.DatabaseType);
                var dbHelper = dbType == DatabaseType.Sqlite
                    ? CreateSqliteDbHelper(config)
                    : CreateDbHelper(dbType, config, config.Database);

                if (LooksLikeQueryStatement(sql))
                {
                    var resultArray = await dbHelper.QueryArrayAsync(sql, null, true);
                    var columns = resultArray.Length > 0
                        ? resultArray[0].Select(FormatQueryCellValue).ToList()
                        : new List<string>();

                    var rows = resultArray.Skip(1)
                                          .Select(row => row.Select(FormatQueryCellValue).ToList())
                                          .ToList();

                    return (true, true, columns, rows, rows.Count, $"Query returned {rows.Count} rows.");
                }

                var affectedRows = await dbHelper.ExecuteAsync(sql);
                return (true, false, new List<string>(), new List<List<string>>(), affectedRows,
                    $"Statement executed successfully. Affected rows: {affectedRows}.");
            }
            catch (NotSupportedException ex)
            {
                return (false, false, new List<string>(), new List<List<string>>(), 0, ex.Message);
            }
            catch (Exception ex)
            {
                return (false, false, new List<string>(), new List<List<string>>(), 0, $"SQL execution failed: {ex.Message}");
            }
        }

        public async Task<(bool Success, long RowCount, DateTime? CreateTime, DateTime? ModifyTime, string Message)>
            GetTableStatisticsAsync(
                ConnectionConfig config,
                string schemaName,
                string tableName)
        {
            try
            {
                if (config == null)
                {
                    return (false, 0, null, null, "Database configuration cannot be null.");
                }

                if (string.IsNullOrWhiteSpace(schemaName) || string.IsNullOrWhiteSpace(tableName))
                {
                    return (false, 0, null, null, "Schema name and table name are required.");
                }

                var dbType = MapDatabaseType(config.DatabaseType);
                var dbHelper = dbType == DatabaseType.Sqlite
                    ? CreateSqliteDbHelper(config)
                    : CreateDbHelper(dbType, config, config.Database);
                var dbBridge = CreateDbBridge(dbType, config);

                var rowCount = await LoadExactRowCountAsync(dbHelper, config.DatabaseType, schemaName, tableName);
                var timestamp = await dbBridge.GetTableTimestampAsync(schemaName, tableName);
                var createTime = timestamp?.CreateTime;
                var modifyTime = timestamp?.ModifyTime;

                return (true, rowCount, createTime, modifyTime, "Loaded table statistics.");
            }
            catch (NotSupportedException ex)
            {
                return (false, 0, null, null, ex.Message);
            }
            catch (Exception ex)
            {
                return (false, 0, null, null, $"Failed to load table statistics: {ex.Message}");
            }
        }

        private static bool LooksLikeQueryStatement(string sql)
        {
            var trimmedSql = sql.TrimStart();
            if (string.IsNullOrWhiteSpace(trimmedSql))
            {
                return false;
            }

            var firstToken = trimmedSql.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries)[0]
                                       .Trim()
                                       .ToLowerInvariant();

            return firstToken is "select" or "with" or "show" or "describe" or "desc" or "explain" or "pragma";
        }

        private static string FormatQueryCellValue(object? value)
        {
            return value switch
            {
                null => string.Empty,
                DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                byte[] bytes => BitConverter.ToString(bytes),
                _ => value.ToString() ?? string.Empty
            };
        }

        private async Task<long> LoadExactRowCountAsync(
            IDbHelper dbHelper,
            DatabaseType databaseType,
            string schemaName,
            string tableName)
        {
            var qualifiedTableName = BuildQualifiedTableName(databaseType, schemaName, tableName);
            var sql = $"SELECT COUNT(1) FROM {qualifiedTableName};";
            var rowCount = await dbHelper.QueryScalarAsync<long?>(sql);
            return rowCount ?? 0;
        }

        private string BuildQualifiedTableName(
            DatabaseType databaseType,
            string schemaName,
            string tableName)
        {
            var quotedTableName = QuoteIdentifier(databaseType, tableName);
            if (string.IsNullOrWhiteSpace(schemaName))
            {
                return quotedTableName;
            }

            return $"{QuoteIdentifier(databaseType, schemaName)}.{quotedTableName}";
        }

        private string BuildQualifiedColumnName(
            DatabaseType databaseType,
            string schemaName,
            string tableName,
            string columnName)
        {
            return $"{BuildQualifiedTableName(databaseType, schemaName, tableName)}.{QuoteIdentifier(databaseType, columnName)}";
        }

        private string QuoteIdentifier(DatabaseType databaseType, string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new ArgumentException("Identifier cannot be null or whitespace.", nameof(identifier));
            }

            return databaseType switch
            {
                DatabaseType.SqlServer => $"[{identifier.Replace("]", "]]")}]",
                DatabaseType.MySql => $"`{identifier.Replace("`", "``")}`",
                _ => $"\"{identifier.Replace("\"", "\"\"")}\""
            };
        }

        private async Task<MySqlColumnDefinitionRow?> LoadMySqlColumnDefinitionAsync(
            IDbHelper dbHelper,
            string schemaName,
            string tableName,
            string columnName)
        {
            const string sql = @"
SELECT COLUMN_NAME AS ColumnName,
       COLUMN_TYPE AS ColumnType,
       IS_NULLABLE AS IsNullable,
       COLUMN_DEFAULT AS ColumnDefault,
       EXTRA AS Extra,
       CHARACTER_SET_NAME AS CharacterSetName,
       COLLATION_NAME AS CollationName,
       GENERATION_EXPRESSION AS GenerationExpression
FROM information_schema.columns
WHERE table_schema = @schema_name
  AND table_name = @table_name
  AND column_name = @column_name
LIMIT 1;";

            return await dbHelper.QueryFirstOrDefaultAsync<MySqlColumnDefinitionRow>(sql,
                new
                {
                    schema_name = schemaName,
                    table_name = tableName,
                    column_name = columnName
                });
        }

        private string BuildMySqlModifyColumnCommentSql(
            string schemaName,
            string tableName,
            MySqlColumnDefinitionRow columnDefinition,
            string comment)
        {
            var builder = new System.Text.StringBuilder();
            builder.Append("ALTER TABLE ");
            builder.Append(BuildQualifiedTableName(DatabaseType.MySql, schemaName, tableName));
            builder.Append(" MODIFY COLUMN ");
            builder.Append(QuoteIdentifier(DatabaseType.MySql, columnDefinition.ColumnName));
            builder.Append(' ');
            builder.Append(columnDefinition.ColumnType);

            AppendMySqlCharacterSetClause(builder, columnDefinition.CharacterSetName, columnDefinition.CollationName);

            builder.Append(columnDefinition.IsNullable.Equals("YES", StringComparison.OrdinalIgnoreCase)
                ? " NULL"
                : " NOT NULL");

            builder.Append(BuildMySqlDefaultClause(columnDefinition));
            builder.Append(BuildMySqlExtraClause(columnDefinition.Extra));
            builder.Append(" COMMENT '");
            builder.Append(EscapeSqlLiteral(comment));
            builder.Append("';");

            return builder.ToString();
        }

        private void AppendMySqlCharacterSetClause(
            System.Text.StringBuilder builder,
            string? characterSetName,
            string? collationName)
        {
            if (!string.IsNullOrWhiteSpace(characterSetName))
            {
                builder.Append(" CHARACTER SET ");
                builder.Append(characterSetName);
            }

            if (!string.IsNullOrWhiteSpace(collationName))
            {
                builder.Append(" COLLATE ");
                builder.Append(collationName);
            }
        }

        private string BuildMySqlDefaultClause(MySqlColumnDefinitionRow columnDefinition)
        {
            if (columnDefinition.ColumnDefault == null)
            {
                return columnDefinition.IsNullable.Equals("YES", StringComparison.OrdinalIgnoreCase)
                    ? " DEFAULT NULL"
                    : string.Empty;
            }

            if (string.Equals(columnDefinition.ColumnDefault, "NULL", StringComparison.OrdinalIgnoreCase))
            {
                return " DEFAULT NULL";
            }

            if (LooksLikeSqlExpression(columnDefinition.ColumnDefault) ||
                (LooksLikeNumber(columnDefinition.ColumnDefault) && !IsMySqlStringLikeColumn(columnDefinition.ColumnType)))
            {
                return $" DEFAULT {columnDefinition.ColumnDefault}";
            }

            return $" DEFAULT '{EscapeSqlLiteral(columnDefinition.ColumnDefault)}'";
        }

        private string BuildMySqlExtraClause(string? extra)
        {
            if (string.IsNullOrWhiteSpace(extra))
            {
                return string.Empty;
            }

            var normalized = NormalizeWhitespace(extra);
            var parts = new List<string>();

            if (normalized.Contains("auto_increment", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add("AUTO_INCREMENT");
            }

            var onUpdateIndex = normalized.IndexOf("on update", StringComparison.OrdinalIgnoreCase);
            if (onUpdateIndex >= 0)
            {
                parts.Add(normalized[onUpdateIndex..]);
            }

            return parts.Count == 0
                ? string.Empty
                : $" {string.Join(" ", parts)}";
        }

        private static bool IsMySqlGeneratedColumn(MySqlColumnDefinitionRow columnDefinition)
        {
            return !string.IsNullOrWhiteSpace(columnDefinition.GenerationExpression) ||
                   (!string.IsNullOrWhiteSpace(columnDefinition.Extra) &&
                    (columnDefinition.Extra.Contains("VIRTUAL GENERATED", StringComparison.OrdinalIgnoreCase) ||
                     columnDefinition.Extra.Contains("STORED GENERATED", StringComparison.OrdinalIgnoreCase)));
        }

        private static string NormalizeComment(string? comment) => comment?.Trim() ?? string.Empty;

        private static string EscapeSqlLiteral(string value) => value.Replace("'", "''");

        private static string NormalizeWhitespace(string value)
        {
            return string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }

        private static bool LooksLikeSqlExpression(string value)
        {
            return value.Equals("CURRENT_TIMESTAMP", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("CURRENT_TIMESTAMP()", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("NOW()", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("UUID()", StringComparison.OrdinalIgnoreCase) ||
                   value.StartsWith("(", StringComparison.Ordinal) ||
                   value.StartsWith("nextval(", StringComparison.OrdinalIgnoreCase);
        }

        private static bool LooksLikeNumber(string value)
        {
            return decimal.TryParse(value, out _) ||
                   value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("false", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMySqlStringLikeColumn(string columnType)
        {
            return columnType.Contains("char", StringComparison.OrdinalIgnoreCase) ||
                   columnType.Contains("text", StringComparison.OrdinalIgnoreCase) ||
                   columnType.Contains("enum", StringComparison.OrdinalIgnoreCase) ||
                   columnType.Contains("set", StringComparison.OrdinalIgnoreCase) ||
                   columnType.Contains("json", StringComparison.OrdinalIgnoreCase);
        }

        private sealed class MySqlColumnDefinitionRow
        {
            public string ColumnName { get; set; } = string.Empty;

            public string ColumnType { get; set; } = string.Empty;

            public string IsNullable { get; set; } = "YES";

            public string? ColumnDefault { get; set; }

            public string? Extra { get; set; }

            public string? CharacterSetName { get; set; }

            public string? CollationName { get; set; }

            public string? GenerationExpression { get; set; }
        }

        /// <summary>
        /// 加载完整的数据库树形结构
        /// </summary>
        /// <param name="config">数据库配置</param>
        /// <returns>根节点</returns>
        public async Task<(bool Success, TreeNodeItem? RootNode, string Message)> LoadDatabaseTreeAsync(ConnectionConfig config)
        {
            try
            {
                if (config == null)
                {
                    return (false, null, "数据库配置不能为空");
                }

                // 创建根节点
                var rootNode = new TreeNodeItem(config.Name, TreeNodeType.Root, "Database")
                               {
                                   DisplayName = config.Name, IsExpanded = true
                               };

                // 获取所有 Schema
                var schemaResult = await GetSchemasAsync(config);
                if (!schemaResult.Success)
                {
                    return (false, null, schemaResult.Message);
                }

                // 创建 Schema 集合节点
                var schemasFolderNode = new TreeNodeItem("Schemas", TreeNodeType.Folder, "Folder")
                                        {
                                            DisplayName = $"架构 ({schemaResult.Schemas.Count})", IsExpanded = true
                                        };
                rootNode.AddChild(schemasFolderNode);

                // 为每个 Schema 加载表
                foreach (var schema in schemaResult.Schemas)
                {
                    var schemaNode = new TreeNodeItem(schema.Name, TreeNodeType.Schema, "Schema")
                                     {
                                         DisplayName = schema.Name, Data = schema
                                     };

                    // 获取该 Schema 下的表
                    var tablesResult = await GetTablesAsync(config, schema.Name);
                    if (tablesResult.Success)
                    {
                        foreach (var table in tablesResult.Tables)
                        {
                            var tableNode = new TreeNodeItem(table.Name, TreeNodeType.Table, "Table")
                                            {
                                                DisplayName = table.Name, Data = table
                                            };
                            schemaNode.AddChild(tableNode);
                        }
                    }

                    schemasFolderNode.AddChild(schemaNode);
                }

                // 创建 Views 集合节点
                var viewsFolderNode = new TreeNodeItem("Views", TreeNodeType.Folder, "Folder")
                                      {
                                          DisplayName = "视图 (0)", IsExpanded = false
                                      };

                // 加载所有视图（从第一个 Schema 或默认 Schema）
                if (schemaResult.Schemas.Count > 0)
                {
                    var totalViewCount = 0;
                    foreach (var schema in schemaResult.Schemas)
                    {
                        var viewsResult = await GetViewsAsync(config, schema.Name);
                        if (!viewsResult.Success || viewsResult.Views.Count == 0)
                        {
                            continue;
                        }

                        foreach (var view in viewsResult.Views.OrderBy(v => v.Name))
                        {
                            var viewNode = new TreeNodeItem(view.Name, TreeNodeType.View, "View")
                                           {
                                               DisplayName = $"{view.Schema}.{view.Name}", Data = view
                                           };
                            viewsFolderNode.AddChild(viewNode);
                        }

                        totalViewCount += viewsResult.Views.Count;
                    }

                    viewsFolderNode.DisplayName = $"视图 ({totalViewCount})";
                }

                rootNode.AddChild(viewsFolderNode);

                // 创建 Stored Procedures 集合节点
                var proceduresFolderNode = new TreeNodeItem("Stored Procedures", TreeNodeType.Folder, "Folder")
                                           {
                                               DisplayName = "存储过程 (0)", IsExpanded = false
                                           };

                // 加载所有存储过程（从第一个 Schema 或默认 Schema）
                if (schemaResult.Schemas.Count > 0)
                {
                    var totalProcedureCount = 0;
                    foreach (var schema in schemaResult.Schemas)
                    {
                        var proceduresResult = await GetStoredProceduresAsync(config, schema.Name);
                        if (!proceduresResult.Success || proceduresResult.Procedures.Count == 0)
                        {
                            continue;
                        }

                        foreach (var procedure in proceduresResult.Procedures.OrderBy(p => p.Name))
                        {
                            var procNode = new TreeNodeItem(procedure.Name, TreeNodeType.StoredProcedure, "StoredProcedure")
                                           {
                                               DisplayName = $"{procedure.Schema}.{procedure.Name}", Data = procedure
                                           };
                            proceduresFolderNode.AddChild(procNode);
                        }

                        totalProcedureCount += proceduresResult.Procedures.Count;
                    }

                    proceduresFolderNode.DisplayName = $"存储过程 ({totalProcedureCount})";
                }

                rootNode.AddChild(proceduresFolderNode);

                return (true, rootNode, $"成功加载数据库树形结构");
            }
            catch (NotSupportedException ex)
            {
                return (false, null, $"不支持的数据库类型: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, null, $"加载数据库树形结构失败: {ex.Message}");
            }
        }
    }
}
