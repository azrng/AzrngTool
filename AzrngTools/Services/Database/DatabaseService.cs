using System.Data;
using Azrng.Core.Model;
using Azrng.Core.Results;
using Azrng.DataAccess;
using Azrng.DataAccess.DbBridge;
using Azrng.DataAccess.Helper;
using AzrngTools.Models.Database;

namespace AzrngTools.Services.Database
{
    /// <summary>
    /// 数据库服务
    /// </summary>
    public class DatabaseService : IDatabaseService, ISingletonDependency
    {
        private readonly object _bridgeCacheLock = new();
        private string? _cachedConnectionKey;
        private IBasicDbBridge? _cachedBridge;

        /// <summary>
        /// 构造用于桥接器缓存命中的连接标识键。
        /// 运行时连接（<see cref="DatabaseConnectionContextService.CreateRuntimeConnection"/>）每次都会创建新副本，
        /// 因此这里必须按连接标识字段比较，而不是引用相等，否则缓存永远无法命中。
        /// </summary>
        internal static string BuildConnectionKey(DatabaseType dbType, ConnectionConfig config)
        {
            return $"{dbType}|{config.Host}|{config.Port}|{config.Database}|{config.Username}|{config.Password}|{config.UseWindowsAuthentication}";
        }

        /// <summary>
        /// 获取或创建数据库桥接器，相同连接标识复用实例
        /// </summary>
        private IBasicDbBridge GetOrCreateDbBridge(ConnectionConfig config)
        {
            var dbType = MapDatabaseType(config.DatabaseType);
            var connectionKey = BuildConnectionKey(dbType, config);

            lock (_bridgeCacheLock)
            {
                if (_cachedBridge != null &&
                    string.Equals(_cachedConnectionKey, connectionKey, StringComparison.Ordinal))
                {
                    return _cachedBridge;
                }

                var bridge = CreateDbBridge(dbType, config);
                _cachedConnectionKey = connectionKey;
                _cachedBridge = bridge;
                return bridge;
            }
        }

        /// <summary>
        /// 连接配置变更时清除缓存
        /// </summary>
        public void InvalidateCache()
        {
            lock (_bridgeCacheLock)
            {
                _cachedConnectionKey = null;
                _cachedBridge = null;
            }
        }
        /// <summary>
        /// 测试数据库连接
        /// </summary>
        /// <param name="config">数据库配置</param>
        /// <returns>连接测试结果</returns>
        public async Task<IResultModel<DatabaseConnectionTestResult>> TestConnectionAsync(ConnectionConfig? config)
        {
            try
            {
                if (config == null)
                {
                    return CreateConnectionTestFailure("数据库配置不能为空", "请检查连接配置是否正确");
                }

                // 验证必填字段
                if (string.IsNullOrWhiteSpace(config.Name))
                {
                    return CreateConnectionTestFailure("连接名称不能为空", "请输入连接名称");
                }


                if (config.DatabaseType != DatabaseType.Sqlite)
                {
                    if (string.IsNullOrWhiteSpace(config.Host))
                    {
                        return CreateConnectionTestFailure("服务器地址不能为空", "请输入数据库服务器地址");
                    }

                    if (config.Port <= 0)
                    {
                        return CreateConnectionTestFailure("端口号无效",
                            $"请输入有效的端口号（{GetDatabaseTypeName(config.DatabaseType)} 默认端口：{GetDefaultPort(config.DatabaseType)}）");
                    }

                    var requiresUsername = !(config.DatabaseType == DatabaseType.SqlServer && config.UseWindowsAuthentication);

                    if (requiresUsername && string.IsNullOrWhiteSpace(config.Username))
                    {
                        return CreateConnectionTestFailure("用户名不能为空", "请输入数据库用户名");
                    }
                }
                else
                {
                    // Sqlite 特殊验证
                    if (string.IsNullOrWhiteSpace(config.Database))
                    {
                        return CreateConnectionTestFailure("Sqlite 数据库文件路径不能为空", "请选择 Sqlite 数据库文件");
                    }

                    if (!File.Exists(config.Database))
                    {
                        return CreateConnectionTestFailure($"Sqlite 数据库文件不存在: {config.Database}", "请检查文件路径是否正确，或选择已存在的数据库文件");
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
                        return CreateConnectionTestSuccess();
                    }
                    else
                    {
                        return CreateConnectionTestFailure("Sqlite 连接测试失败", "请检查数据库文件是否有效且未被占用");
                    }
                }

                if (config.DatabaseType == DatabaseType.SqlServer && config.UseWindowsAuthentication)
                {
                    await using var sqlConnection = new Microsoft.Data.SqlClient.SqlConnection(BuildConnectionString(config));
                    await sqlConnection.OpenAsync();
                    await using var command = new Microsoft.Data.SqlClient.SqlCommand("SELECT 1", sqlConnection);
                    await command.ExecuteScalarAsync();

                    LoggingService.LogOperation($"测试 SQL Server Windows 身份认证连接成功: {config.Name}");
                    return CreateConnectionTestSuccess();
                }

                var testConfig = CreateCatalogConnectionConfig(config);
                var dbBridge = CreateDbBridge(dbType, testConfig);

                // 调用获取 Schema 列表来测试连接
                _ = await dbBridge.GetSchemaListAsync();

                LoggingService.LogOperation($"测试数据库连接成功: {config.Name} ({config.DatabaseType})");
                return CreateConnectionTestSuccess();
            }
            catch (NotSupportedException ex)
            {
                LoggingService.LogError($"不支持的数据库类型: {config?.DatabaseType}", ex);
                return CreateConnectionTestFailure($"不支持的数据库类型: {config?.DatabaseType}", "当前版本暂不支持此数据库类型", ex);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"连接测试失败: {config?.Name}", ex);
                return CreateConnectionTestFailure($"连接测试失败: {ex.Message}", GetConnectionErrorSuggestion(ex.Message), ex);
            }
        }

        private static IResultModel<DatabaseConnectionTestResult> CreateConnectionTestSuccess()
        {
            return ResultModelFactory.Success(new DatabaseConnectionTestResult(null), "连接成功");
        }

        private static IResultModel<DatabaseConnectionTestResult> CreateConnectionTestFailure(
            string message,
            string? suggestion,
            Exception? exception = null)
        {
            var result = exception == null
                ? ResultModelFactory.Failure<DatabaseConnectionTestResult>(message, "DATABASE_ERROR")
                : ResultModelFactory.FromException<DatabaseConnectionTestResult>(exception, message);
            result.Data = new DatabaseConnectionTestResult(suggestion);
            return result;
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
        /// 构建连接字符串。
        /// 使用各数据库官方 <c>ConnectionStringBuilder</c> 组装，避免手工拼接时 Host / 用户名含
        /// <c>;</c> 等特殊字符破坏连接字符串，或注入额外参数。
        /// </summary>
        /// <param name="config">数据库配置</param>
        /// <returns>连接字符串</returns>
        private static string BuildConnectionString(ConnectionConfig config)
        {
            return config.DatabaseType switch
            {
                DatabaseType.SqlServer => BuildSqlServerConnectionString(config),
                DatabaseType.MySql => BuildMySqlConnectionString(config),
                DatabaseType.PostgresSql => BuildPostgresConnectionString(config),
                DatabaseType.Oracle => BuildOracleConnectionString(config),
                DatabaseType.Sqlite => BuildSqliteConnectionString(config),
                // 达梦当前未接入桥接器（见 MapDatabaseType），保留显式抛错避免误用 SQL Server 逻辑
                DatabaseType.Dm => throw new NotSupportedException($"不支持的数据库类型: {config.DatabaseType}"),
                _ => throw new NotSupportedException($"不支持的数据库类型: {config.DatabaseType}")
            };
        }

        private static string BuildSqlServerConnectionString(ConnectionConfig config)
        {
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
            {
                DataSource = $"{config.Host},{config.Port}",
                InitialCatalog = config.Database,
                TrustServerCertificate = true
            };

            if (config.UseWindowsAuthentication)
            {
                builder.IntegratedSecurity = true;
            }
            else
            {
                builder.UserID = config.Username;
                builder.Password = config.Password;
            }

            return builder.ConnectionString;
        }

        private static string BuildMySqlConnectionString(ConnectionConfig config)
        {
            var builder = new MySqlConnector.MySqlConnectionStringBuilder
            {
                Server = config.Host,
                Port = (uint)config.Port,
                Database = config.Database,
                UserID = config.Username,
                Password = config.Password
            };

            return builder.ConnectionString;
        }

        private static string BuildPostgresConnectionString(ConnectionConfig config)
        {
            var builder = new Npgsql.NpgsqlConnectionStringBuilder
            {
                Host = config.Host,
                Port = config.Port,
                Database = config.Database,
                Username = config.Username,
                Password = config.Password
            };

            return builder.ConnectionString;
        }

        private static string BuildOracleConnectionString(ConnectionConfig config)
        {
            // Oracle 采用 EZCONNECT/TNS 描述符形式，官方 builder 以 USER ID/PASSWORD + 数据源描述组装，
            // 这里复用驱动连接串 builder 的键值分隔与转义能力，避免手工拼接注入。
            var builder = new Oracle.ManagedDataAccess.Client.OracleConnectionStringBuilder
            {
                DataSource =
                    $"(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={config.Host})(PORT={config.Port}))(CONNECT_DATA=(SERVICE_NAME={config.Database})))",
                UserID = config.Username,
                Password = config.Password
            };

            return builder.ConnectionString;
        }

        private static string BuildSqliteConnectionString(ConnectionConfig config)
        {
            var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            {
                DataSource = config.Database
            };

            return builder.ConnectionString;
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
                _ => throw new NotSupportedException($"不支持的数据库类型: {type}")
            };
        }

        private static string GetCatalogDatabaseName(DatabaseType dbType, string currentDatabaseName)
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
        public async Task<IResultModel<List<string>>> GetDatabaseNamesAsync(ConnectionConfig config)
        {
            try
            {
                if (config == null)
                {
                    return ResultModelFactory.Failure<List<string>>("数据库配置不能为空", "DATABASE_ERROR");
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

                    return ResultModelFactory.Success(windowsAuthDatabases, $"Loaded {windowsAuthDatabases.Count} databases successfully.");
                }

                var catalogConfig = CreateCatalogConnectionConfig(config);
                var dbBridge = GetOrCreateDbBridge(catalogConfig);
                var databases = await dbBridge.GetDatabaseNameListAsync();
                var normalizedDatabases = databases
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return ResultModelFactory.Success(normalizedDatabases, $"成功加载 {normalizedDatabases.Count} 个数据库");
            }
            catch (NotSupportedException ex)
            {
                LoggingService.LogError($"不支持的数据库类型: {config?.DatabaseType}", ex);
                return ex.ToFailureResult<List<string>>(ex.Message);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"加载数据库列表失败: {config?.Name}", ex);
                return ex.ToFailureResult<List<string>>($"加载数据库列表失败: {ex.Message}");
            }
        }

        internal static ConnectionConfig CreateCatalogConnectionConfig(ConnectionConfig config)
        {
            var catalogDatabase = GetCatalogDatabaseName(config.DatabaseType, string.Empty);
            if (string.IsNullOrWhiteSpace(catalogDatabase) ||
                string.Equals(catalogDatabase, config.Database, StringComparison.OrdinalIgnoreCase))
            {
                return config;
            }

            return new ConnectionConfig
            {
                Name = config.Name,
                DatabaseType = config.DatabaseType,
                Host = config.Host,
                Port = config.Port,
                Username = config.Username,
                Password = config.Password,
                Database = catalogDatabase,
                UseWindowsAuthentication = config.UseWindowsAuthentication,
                GroupId = config.GroupId,
                GroupName = config.GroupName,
                Color = config.Color
            };
        }

        internal static string GetMySqlRuntimeSchemaName(ConnectionConfig config)
        {
            return string.IsNullOrWhiteSpace(config.Database)
                ? "default"
                : config.Database;
        }

        private static SchemaModel CreateMySqlRuntimeSchema(ConnectionConfig config)
        {
            return new SchemaModel
            {
                Name = GetMySqlRuntimeSchemaName(config),
                Owner = "MySql",
                TableCount = 0,
                IsDefault = true
            };
        }

        /// <summary>
        /// 获取数据库 Schema 列表
        /// </summary>
        /// <param name="config">数据库配置</param>
        /// <returns>Schema 列表</returns>
        public async Task<IResultModel<List<SchemaModel>>> GetSchemasAsync(ConnectionConfig config)
        {
            try
            {
                if (config == null)
                {
                    return ResultModelFactory.Failure<List<SchemaModel>>("数据库配置不能为空", "DATABASE_ERROR");
                }

                var dbType = MapDatabaseType(config.DatabaseType);

                if (dbType == DatabaseType.MySql)
                {
                    var mySqlSchema = CreateMySqlRuntimeSchema(config);
                    return ResultModelFactory.Success(new List<SchemaModel> { mySqlSchema }, "成功加载 1 个 Schema");
                }

                if (dbType == DatabaseType.Sqlite)
                {
                    var sqliteSchemas = new List<SchemaModel>
                                        {
                                            new() { Name = "main", Owner = "Sqlite", TableCount = 0, IsDefault = true }
                                        };

                    return ResultModelFactory.Success(sqliteSchemas, "成功加载 1 个 Schema");
                }

                var dbBridge = GetOrCreateDbBridge(config);

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

                return ResultModelFactory.Success(schemas, $"成功加载 {schemas.Count} 个 Schema");
            }
            catch (NotSupportedException ex)
            {
                LoggingService.LogError($"不支持的数据库类型: {config?.DatabaseType}", ex);
                return ex.ToFailureResult<List<SchemaModel>>($"不支持的数据库类型: {ex.Message}");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"加载 Schema 失败: {config?.Name}", ex);
                return ex.ToFailureResult<List<SchemaModel>>($"加载 Schema 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取指定 Schema 下的表列表
        /// </summary>
        /// <param name="config">数据库配置</param>
        /// <param name="schemaName">Schema 名称</param>
        /// <returns>表列表</returns>
        public async Task<IResultModel<List<TableModel>>> GetTablesAsync(
            ConnectionConfig config, string schemaName)
        {
            try
            {
                if (config == null)
                {
                    return ResultModelFactory.Failure<List<TableModel>>("数据库配置不能为空", "DATABASE_ERROR");
                }

                if (string.IsNullOrWhiteSpace(schemaName))
                {
                    return ResultModelFactory.Failure<List<TableModel>>("Schema 名称不能为空", "DATABASE_ERROR");
                }

                var dbBridge = GetOrCreateDbBridge(config);

                var tableList = await dbBridge.GetTableInfoListAsync(schemaName);

                var tables = tableList.Select(dto => new TableModel
                                                     {
                                                         Name = dto.TableName,
                                                         Schema = schemaName,
                                                         TableType = "TABLE",
                                                         Comment = dto.TableComment ?? string.Empty
                                                     })
                                      .ToList();

                return ResultModelFactory.Success(tables, $"成功加载 {tables.Count} 个表");
            }
            catch (NotSupportedException ex)
            {
                LoggingService.LogError($"不支持的数据库类型: {config?.DatabaseType}", ex);
                return ex.ToFailureResult<List<TableModel>>($"不支持的数据库类型: {ex.Message}");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"加载表失败: {config?.Name}.{schemaName}", ex);
                return ex.ToFailureResult<List<TableModel>>($"加载表失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取指定表的列信息
        /// </summary>
        /// <param name="config">数据库配置</param>
        /// <param name="schemaName">Schema 名称</param>
        /// <param name="tableName">表名</param>
        /// <returns>列列表</returns>
        public async Task<IResultModel<List<ColumnModel>>> GetColumnsAsync(
            ConnectionConfig config, string schemaName, string tableName)
        {
            try
            {
                if (config == null)
                {
                    return ResultModelFactory.Failure<List<ColumnModel>>("数据库配置不能为空", "DATABASE_ERROR");
                }

                if (string.IsNullOrWhiteSpace(schemaName))
                {
                    return ResultModelFactory.Failure<List<ColumnModel>>("Schema 名称不能为空", "DATABASE_ERROR");
                }

                if (string.IsNullOrWhiteSpace(tableName))
                {
                    return ResultModelFactory.Failure<List<ColumnModel>>("表名不能为空", "DATABASE_ERROR");
                }

                var dbBridge = GetOrCreateDbBridge(config);

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

                return ResultModelFactory.Success(columns, $"成功加载 {columns.Count} 个列");
            }
            catch (NotSupportedException ex)
            {
                LoggingService.LogError($"不支持的数据库类型: {config?.DatabaseType}", ex);
                return ex.ToFailureResult<List<ColumnModel>>($"不支持的数据库类型: {ex.Message}");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"加载列失败: {config?.Name}.{schemaName}.{tableName}", ex);
                return ex.ToFailureResult<List<ColumnModel>>($"加载列失败: {ex.Message}");
            }
        }

        public async Task<IResultModel<bool>> UpdateTableCommentAsync(
            ConnectionConfig config,
            string schemaName,
            string tableName,
            string? comment)
        {
            try
            {
                if (config == null)
                {
                    return ResultModelFactory.Failure<bool>("数据库配置不能为空", "DATABASE_ERROR");
                }

                if (string.IsNullOrWhiteSpace(tableName))
                {
                    return ResultModelFactory.Failure<bool>("表名不能为空", "DATABASE_ERROR");
                }

                var dbType = MapDatabaseType(config.DatabaseType);
                if (dbType is not (DatabaseType.PostgresSql or DatabaseType.MySql))
                {
                    return ResultModelFactory.Failure<bool>("当前连接类型暂不支持修改备注", "DATABASE_ERROR");
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
                return ResultModelFactory.Success(true, "表备注已更新");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"更新表备注失败：{schemaName}.{tableName}", ex);
                return ex.ToFailureResult<bool>($"更新表备注失败：{ex.Message}");
            }
        }

        public async Task<IResultModel<bool>> UpdateColumnCommentAsync(
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
                    return ResultModelFactory.Failure<bool>("数据库配置不能为空", "DATABASE_ERROR");
                }

                if (string.IsNullOrWhiteSpace(tableName))
                {
                    return ResultModelFactory.Failure<bool>("表名不能为空", "DATABASE_ERROR");
                }

                if (string.IsNullOrWhiteSpace(columnName))
                {
                    return ResultModelFactory.Failure<bool>("字段名不能为空", "DATABASE_ERROR");
                }

                var dbType = MapDatabaseType(config.DatabaseType);
                if (dbType is not (DatabaseType.PostgresSql or DatabaseType.MySql))
                {
                    return ResultModelFactory.Failure<bool>("当前连接类型暂不支持修改备注", "DATABASE_ERROR");
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
                    return ResultModelFactory.Success(true, "字段备注已更新");
                }

                var columnDefinition = await LoadMySqlColumnDefinitionAsync(dbHelper, schemaName, tableName, columnName);
                if (columnDefinition == null)
                {
                    return ResultModelFactory.Failure<bool>("无法获取字段当前定义，已取消保存", "DATABASE_ERROR");
                }

                if (IsMySqlGeneratedColumn(columnDefinition))
                {
                    return ResultModelFactory.Failure<bool>("暂不支持修改生成列备注", "DATABASE_ERROR");
                }

                if (!TryBuildMySqlModifyColumnCommentSql(
                        schemaName,
                        tableName,
                        columnDefinition,
                        normalizedComment,
                        out var modifySql,
                        out var failureMessage))
                {
                    LoggingService.LogWarning(failureMessage);
                    return ResultModelFactory.Failure<bool>(failureMessage, "DATABASE_ERROR");
                }

                await dbHelper.ExecuteAsync(modifySql);
                return ResultModelFactory.Success(true, "字段备注已更新");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"更新字段备注失败：{schemaName}.{tableName}.{columnName}", ex);
                return ex.ToFailureResult<bool>($"更新字段备注失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 获取指定 Schema 下的视图列表
        /// </summary>
        /// <param name="config">数据库配置</param>
        /// <param name="schemaName">Schema 名称</param>
        /// <returns>视图列表</returns>
        public async Task<IResultModel<List<ViewModel>>> GetViewsAsync(ConnectionConfig config, string schemaName)
        {
            try
            {
                if (config == null)
                {
                    return ResultModelFactory.Failure<List<ViewModel>>("数据库配置不能为空", "DATABASE_ERROR");
                }

                if (string.IsNullOrWhiteSpace(schemaName))
                {
                    return ResultModelFactory.Failure<List<ViewModel>>("Schema 名称不能为空", "DATABASE_ERROR");
                }

                var dbBridge = GetOrCreateDbBridge(config);

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

                return ResultModelFactory.Success(views, $"成功加载 {views.Count} 个视图");
            }
            catch (NotSupportedException ex)
            {
                LoggingService.LogError($"不支持的数据库类型: {config?.DatabaseType}", ex);
                return ex.ToFailureResult<List<ViewModel>>($"不支持的数据库类型: {ex.Message}");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"加载视图失败: {config?.Name}.{schemaName}", ex);
                return ex.ToFailureResult<List<ViewModel>>($"加载视图失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取指定 Schema 下的存储过程列表
        /// </summary>
        /// <param name="config">数据库配置</param>
        /// <param name="schemaName">Schema 名称</param>
        /// <returns>存储过程列表</returns>
        public async Task<IResultModel<List<StoredProcedureModel>>> GetStoredProceduresAsync(
            ConnectionConfig config, string schemaName)
        {
            try
            {
                if (config == null)
                {
                    return ResultModelFactory.Failure<List<StoredProcedureModel>>("数据库配置不能为空", "DATABASE_ERROR");
                }

                if (string.IsNullOrWhiteSpace(schemaName))
                {
                    return ResultModelFactory.Failure<List<StoredProcedureModel>>("Schema 名称不能为空", "DATABASE_ERROR");
                }

                var dbType = MapDatabaseType(config.DatabaseType);

                if (dbType == DatabaseType.Sqlite)
                {
                    return ResultModelFactory.Success(new List<StoredProcedureModel>(), "Sqlite 不支持存储过程");
                }

                var dbBridge = GetOrCreateDbBridge(config);
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

                return ResultModelFactory.Success(procedures, $"成功加载 {procedures.Count} 个存储过程");
            }
            catch (NotSupportedException ex)
            {
                LoggingService.LogError($"不支持的数据库类型: {config?.DatabaseType}", ex);
                return ex.ToFailureResult<List<StoredProcedureModel>>($"不支持的数据库类型: {ex.Message}");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"加载存储过程失败: {config?.Name}.{schemaName}", ex);
                return ex.ToFailureResult<List<StoredProcedureModel>>($"加载存储过程失败: {ex.Message}");
            }
        }

        public async Task<IResultModel<List<StoredProcedureModel>>> GetFunctionsAsync(
            ConnectionConfig config, string schemaName)
        {
            try
            {
                if (config == null)
                {
                    return ResultModelFactory.Failure<List<StoredProcedureModel>>("数据库配置不能为空", "DATABASE_ERROR");
                }

                if (string.IsNullOrWhiteSpace(schemaName))
                {
                    return ResultModelFactory.Failure<List<StoredProcedureModel>>("Schema 名称不能为空", "DATABASE_ERROR");
                }

                var dbType = MapDatabaseType(config.DatabaseType);
                if (dbType != DatabaseType.MySql)
                {
                    return ResultModelFactory.Success(new List<StoredProcedureModel>(), "当前数据库类型暂无函数列表");
                }

                var dbBridge = GetOrCreateDbBridge(config);
                var routineList = await dbBridge.GetSchemaRoutineListAsync(schemaName);
                var functions = routineList
                    .Where(dto => string.Equals(dto.RoutineType, "FUNCTION", StringComparison.OrdinalIgnoreCase))
                    .Select(dto => new StoredProcedureModel
                    {
                        Name = dto.RoutineName,
                        Schema = dto.SchemaName,
                        Definition = dto.RoutineDefinition ?? string.Empty,
                        Parameters = $"{dto.InputParam ?? ""} {dto.OutputParam ?? ""}".Trim(),
                        Comment = dto.RoutineDescription ?? string.Empty,
                        RoutineType = dto.RoutineType ?? "FUNCTION"
                    })
                    .OrderBy(function => function.Name)
                    .ToList();

                return ResultModelFactory.Success(functions, $"成功加载 {functions.Count} 个函数");
            }
            catch (NotSupportedException ex)
            {
                LoggingService.LogError($"不支持的数据库类型: {config?.DatabaseType}", ex);
                return ex.ToFailureResult<List<StoredProcedureModel>>($"不支持的数据库类型: {ex.Message}");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"加载函数失败: {config?.Name}.{schemaName}", ex);
                return ex.ToFailureResult<List<StoredProcedureModel>>($"加载函数失败: {ex.Message}");
            }
        }

        public async Task<IResultModel<string>> GetViewDefinitionAsync(
            ConnectionConfig config,
            string schemaName,
            string viewName)
        {
            try
            {
                if (config == null)
                {
                    return ResultModelFactory.Failure<string>("数据库配置不能为空", "DATABASE_ERROR");
                }

                if (string.IsNullOrWhiteSpace(schemaName) || string.IsNullOrWhiteSpace(viewName))
                {
                    return ResultModelFactory.Failure<string>("视图信息不能为空", "DATABASE_ERROR");
                }

                var dbBridge = GetOrCreateDbBridge(config);
                var view = await dbBridge.GetSchemaViewAsync(schemaName, viewName);
                var ddl = view?.ViewDefinition;
                return string.IsNullOrWhiteSpace(ddl)
                    ? ResultModelFactory.Failure<string>("未查询到视图定义", "DATABASE_ERROR")
                    : ResultModelFactory.Success(ddl!, "成功加载视图定义");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"加载视图定义失败: {config?.Name}.{schemaName}.{viewName}", ex);
                return ex.ToFailureResult<string>($"加载视图定义失败: {ex.Message}");
            }
        }

        public async Task<IResultModel<string>> GetStoredProcedureDefinitionAsync(
            ConnectionConfig config,
            string schemaName,
            string procedureName)
        {
            try
            {
                if (config == null)
                {
                    return ResultModelFactory.Failure<string>("数据库配置不能为空", "DATABASE_ERROR");
                }

                if (string.IsNullOrWhiteSpace(schemaName) || string.IsNullOrWhiteSpace(procedureName))
                {
                    return ResultModelFactory.Failure<string>("存储过程信息不能为空", "DATABASE_ERROR");
                }

                var dbType = MapDatabaseType(config.DatabaseType);
                if (dbType == DatabaseType.Sqlite)
                {
                    return ResultModelFactory.Failure<string>("Sqlite 不支持存储过程", "DATABASE_ERROR");
                }

                var dbBridge = GetOrCreateDbBridge(config);
                var routine = await dbBridge.GetSchemaRoutineAsync(schemaName, procedureName);
                var ddl = routine?.RoutineDefinition;
                return string.IsNullOrWhiteSpace(ddl)
                    ? ResultModelFactory.Failure<string>("未查询到存储过程定义", "DATABASE_ERROR")
                    : ResultModelFactory.Success(ddl!, "成功加载存储过程定义");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"加载存储过程定义失败: {config?.Name}.{schemaName}.{procedureName}", ex);
                return ex.ToFailureResult<string>($"加载存储过程定义失败: {ex.Message}");
            }
        }

        public async Task<IResultModel<List<IndexModel>>> GetIndexesAsync(
            ConnectionConfig config,
            string schemaName,
            string tableName)
        {
            try
            {
                if (config == null)
                {
                    return ResultModelFactory.Failure<List<IndexModel>>("Database configuration cannot be null.", "DATABASE_ERROR");
                }

                if (string.IsNullOrWhiteSpace(schemaName) || string.IsNullOrWhiteSpace(tableName))
                {
                    return ResultModelFactory.Failure<List<IndexModel>>("Schema name and table name are required.", "DATABASE_ERROR");
                }

                var dbBridge = GetOrCreateDbBridge(config);
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

                return ResultModelFactory.Success(indexes, $"Loaded {indexes.Count} indexes.");
            }
            catch (NotSupportedException ex)
            {
                LoggingService.LogError($"不支持的数据库类型: {config?.DatabaseType}", ex);
                return ex.ToFailureResult<List<IndexModel>>(ex.Message);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"加载索引失败: {config?.Name}.{schemaName}.{tableName}", ex);
                return ex.ToFailureResult<List<IndexModel>>($"Failed to load indexes: {ex.Message}");
            }
        }

        public async Task<IResultModel<DatabaseSqlExecutionResult>> ExecuteSqlAsync(
            ConnectionConfig config,
            string sql)
        {
            try
            {
                if (config == null)
                {
                    return ResultModelFactory.Failure<DatabaseSqlExecutionResult>("Database configuration cannot be null.", "DATABASE_ERROR");
                }

                if (string.IsNullOrWhiteSpace(sql))
                {
                    return ResultModelFactory.Failure<DatabaseSqlExecutionResult>("SQL text cannot be empty.", "DATABASE_ERROR");
                }

                var dbType = MapDatabaseType(config.DatabaseType);
                var dbHelper = dbType == DatabaseType.Sqlite
                    ? CreateSqliteDbHelper(config)
                    : CreateDbHelper(dbType, config, config.Database);

                if (LooksLikeQueryStatement(sql))
                {
                    var previewQuery = DatabaseQueryPreviewLimiter.BuildPreviewQuery(dbType, sql, DatabaseQueryPreviewLimiter.DefaultMaxRows);
                    var resultArray = await dbHelper.QueryArrayAsync(previewQuery.Sql, null, true);
                    var columns = resultArray.Length > 0
                        ? resultArray[0].Select(FormatQueryCellValue).ToList()
                        : new List<string>();

                    var rows = resultArray.Skip(1)
                                          .Select(row => row.Select(FormatQueryCellValue).ToList())
                                          .ToList();

                    var message = previewQuery.WasLimited
                        ? $"Query returned first {rows.Count} rows. Preview is limited to {DatabaseQueryPreviewLimiter.DefaultMaxRows} rows."
                        : $"Query returned {rows.Count} rows.";
                    return ResultModelFactory.Success(
                        new DatabaseSqlExecutionResult(true, columns, rows, rows.Count),
                        message);
                }

                var affectedRows = await dbHelper.ExecuteAsync(sql);
                return ResultModelFactory.Success(
                    new DatabaseSqlExecutionResult(false, new List<string>(), new List<List<string>>(), affectedRows),
                    $"Statement executed successfully. Affected rows: {affectedRows}.");
            }
            catch (NotSupportedException ex)
            {
                LoggingService.LogError($"不支持的数据库类型: {config?.DatabaseType}", ex);
                return ex.ToFailureResult<DatabaseSqlExecutionResult>(ex.Message);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"SQL 执行失败: {config?.Name}", ex);
                return ex.ToFailureResult<DatabaseSqlExecutionResult>($"SQL execution failed: {ex.Message}");
            }
        }

        public async Task<IResultModel<DatabaseTableStatisticsResult>>
            GetTableStatisticsAsync(
                ConnectionConfig config,
                string schemaName,
                string tableName)
        {
            try
            {
                if (config == null)
                {
                    return ResultModelFactory.Failure<DatabaseTableStatisticsResult>("Database configuration cannot be null.", "DATABASE_ERROR");
                }

                if (string.IsNullOrWhiteSpace(schemaName) || string.IsNullOrWhiteSpace(tableName))
                {
                    return ResultModelFactory.Failure<DatabaseTableStatisticsResult>("Schema name and table name are required.", "DATABASE_ERROR");
                }

                var dbBridge = GetOrCreateDbBridge(config);

                var timestamp = await dbBridge.GetTableTimestampAsync(schemaName, tableName);
                var createTime = timestamp?.CreateTime;
                var modifyTime = timestamp?.ModifyTime;

                return ResultModelFactory.Success(
                    new DatabaseTableStatisticsResult(-1, createTime, modifyTime),
                    "Loaded table metadata. Exact row count is deferred.");
            }
            catch (NotSupportedException ex)
            {
                LoggingService.LogError($"不支持的数据库类型: {config?.DatabaseType}", ex);
                return ex.ToFailureResult<DatabaseTableStatisticsResult>(ex.Message);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"加载表统计信息失败: {config?.Name}.{schemaName}.{tableName}", ex);
                return ex.ToFailureResult<DatabaseTableStatisticsResult>($"Failed to load table statistics: {ex.Message}");
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

        internal bool TryBuildMySqlModifyColumnCommentSql(
            string schemaName,
            string tableName,
            MySqlColumnDefinitionRow columnDefinition,
            string comment,
            out string sql,
            out string failureMessage)
        {
            sql = string.Empty;
            failureMessage = string.Empty;

            if (!IsSafeMySqlColumnType(columnDefinition.ColumnType))
            {
                failureMessage = $"字段类型暂不支持安全重建，已取消保存字段备注：{SafeDescribeColumnType(columnDefinition.ColumnType)}";
                return false;
            }

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

            sql = builder.ToString();
            return true;
        }

        private void AppendMySqlCharacterSetClause(
            System.Text.StringBuilder builder,
            string? characterSetName,
            string? collationName)
        {
            // 字符集名与排序规则名是 MySQL 标识符，合法字符仅为字母、数字、下划线；
            // 对不在白名单的值跳过对应子句，避免被拼入 ALTER TABLE 语句。
            if (IsSafeMySqlIdentifierName(characterSetName))
            {
                builder.Append(" CHARACTER SET ");
                builder.Append(characterSetName);
            }
            else if (!string.IsNullOrWhiteSpace(characterSetName))
            {
                LoggingService.LogWarning("MySql 字符集名包含非法字符，已跳过 CHARACTER SET 子句。");
            }

            if (IsSafeMySqlIdentifierName(collationName))
            {
                builder.Append(" COLLATE ");
                builder.Append(collationName);
            }
            else if (!string.IsNullOrWhiteSpace(collationName))
            {
                LoggingService.LogWarning("MySql 排序规则名包含非法字符，已跳过 COLLATE 子句。");
            }
        }

        /// <summary>
        /// MySQL 标识符名（字符集名、排序规则名）白名单：仅允许字母、数字、下划线。
        /// </summary>
        private static bool IsSafeMySqlIdentifierName(string? value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.All(ch => char.IsLetterOrDigit(ch) || ch == '_');
        }

        /// <summary>
        /// MySQL 列类型定义白名单：允许字母、数字、下划线、括号、逗号、空格、点。
        /// enum/set 等包含引号的复杂类型暂不重建，避免备注保存误改列定义。
        /// </summary>
        private static bool IsSafeMySqlColumnType(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return value.All(ch =>
                char.IsLetterOrDigit(ch) ||
                ch == '_' || ch == '(' || ch == ')' || ch == ',' || ch == ' ' || ch == '.');
        }

        /// <summary>
        /// 在日志中描述列类型，避免把可能含特殊字符的原始值原样写入日志。
        /// </summary>
        private static string SafeDescribeColumnType(string value)
        {
            return value.Length > 40 ? value[..40] + "…" : value;
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

        internal sealed class MySqlColumnDefinitionRow
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
        /// 加载数据库树形结构骨架，具体对象按节点懒加载
        /// </summary>
        /// <param name="config">数据库配置</param>
        /// <returns>根节点</returns>
        public async Task<IResultModel<TreeNodeItem?>> LoadDatabaseTreeAsync(ConnectionConfig config)
        {
            try
            {
                if (config == null)
                {
                    return ResultModelFactory.Failure<TreeNodeItem?>("数据库配置不能为空", "DATABASE_ERROR");
                }

                var schemaResult = await GetSchemasAsync(config);
                if (!schemaResult.IsSuccess)
                {
                    return ResultModelFactory.Failure<TreeNodeItem?>(schemaResult.Message, "DATABASE_ERROR");
                }

                var rootNode = DatabaseTreeSkeletonBuilder.BuildSkeleton(config.Name, schemaResult.DataOrEmpty());
                return ResultModelFactory.Success<TreeNodeItem?>(rootNode, "成功加载数据库树形结构");
            }
            catch (NotSupportedException ex)
            {
                LoggingService.LogError($"不支持的数据库类型: {config?.DatabaseType}", ex);
                return ex.ToFailureResult<TreeNodeItem?>($"不支持的数据库类型: {ex.Message}");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"加载数据库树形结构失败: {config?.Name}", ex);
                return ex.ToFailureResult<TreeNodeItem?>($"加载数据库树形结构失败: {ex.Message}");
            }
        }
    }
}
