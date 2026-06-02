# Database 模块审查报告

- 审查日期：2026-06-02
- 审查范围：`AzrngTools/Services/Database/`、`AzrngTools/ViewModels/Database/`
- 审查依据：`AGENTS.md`、`application-AGENTS.md`、`infrastructure-AGENTS.md`

---

## 处理状态更新（2026-06-02）

| 问题 | 当前状态 | 处理结论 |
|------|----------|----------|
| `MapDatabaseType` 达梦 / 未知类型映射为 SQL Server | 已处理 | 已改为不支持类型直接抛出 `NotSupportedException`，避免静默走错桥接器，并补充单元测试覆盖 |
| `ExportDialogViewModel.MapExportObjectType` 只支持表 | 已处理 | 已补充 View 与 StoredProcedure 到导出对象类型的防御性映射，并补充单元测试覆盖；当前主导出流程仍以表导出为主，完整导出视图 / 存储过程能力需另开专项 |
| `ValidateFormLegacy` / `ValidateConnectionNameUniqueLegacy` 死代码 | 已处理 | 已删除 Legacy 方法，保留当前实际使用的校验链路 |
| `ShowDatabaseSelector` 重复属性通知 | 已处理 | 已删除重复通知，保留一次状态刷新 |
| Database 模块测试覆盖缺失 | 已改善 | 已补充数据库服务、导出对象映射、导出负载构建、代码生成负载构建、连接配置序列化、DI 注册和 MySql Schema 策略测试；本次回归 `dotnet test` 通过 43 个测试 |
| `DatabaseService` 未走 DI | 已处理 | 已抽出 `IDatabaseService`，`DatabaseService` 通过 `ISingletonDependency` 扫描注册；数据库工作台主 ViewModel 与子 ViewModel 改为构造注入共享服务实例 |
| `MainWindowViewModel` 职责过重 | 需专项 | 属于模块拆分和应用层编排重构，需单独设计拆分边界和回归范围 |
| `DocumentExportService` / `CodeGenerationService` 直接 new | 已处理 | 已抽出 `IDocumentExportService` 与 `ICodeGenerationService`，服务通过 DI 扫描注册，主 ViewModel 改为构造注入 |
| 文档导出负载构建留在主 ViewModel | 已处理 | 已抽出 `IDatabaseExportPayloadService`，主 ViewModel 不再直接编排表字段/索引加载 |
| 代码生成负载构建留在主 ViewModel | 已处理 | 已抽出 `ICodeGenerationPayloadService`，主 ViewModel 不再直接编排代码生成前的表与字段加载 |
| 连接配置读写和密码加密副本逻辑留在主 ViewModel | 已处理 | 已抽出 `IConnectionConfigurationService`，连接配置文件读写、JSON 导入导出和密码加密副本逻辑改由服务层承载 |
| 密码原地加解密序列化 | 已处理 | 保存和导出连接配置时改为序列化加密副本，不再修改内存中的连接密码，并补充测试覆盖 |
| 连接字符串中的密码明文 | 需包级优化 | 当前受 `Azrng.DataAccess` 部分桥接器构造契约限制，已在 `doc/2026-06-02-Azrng.DataAccess-connection-security-notes.md` 记录包级改进建议 |
| fire-and-forget 异常处理 | 已处理 | 连接上下文初始化、数据库切换以及表 / 视图 / 存储过程详情自动加载改为安全调度，异常会记录日志并反馈到界面状态 |
| `ResultModel<T>` / Azrng 异常体系统一 | 需专项 | 涉及服务层返回契约变更和调用方联动，需单独规划 |
| MySql Schema 处理集中化、连接缓存线程安全 | 已处理 | MySql 运行时 Schema 名称由 `DatabaseService` 统一生成，主 ViewModel 不再重复硬编码；桥接器缓存读写已加锁，降低后台调用竞态风险 |

---

## 模块概览

| 文件 | 职责 | 行数 |
|------|------|------|
| `Services/Database/DatabaseService.cs` | 数据库连接、查询、元数据操作 | 1364 |
| `ViewModels/Database/MainWindowViewModel.cs` | 主窗口编排、连接管理、导出、代码生成 | 1687 |
| `ViewModels/Database/ConnectionDialogViewModel.cs` | 连接对话框状态管理、表单验证 | 1134 |
| `ViewModels/Database/TableDetailViewModel.cs` | 表详情展示、备注编辑 | 689 |
| `ViewModels/Database/DatabaseBrowserViewModel.cs` | 对象树浏览、懒加载、搜索 | 596 |
| `ViewModels/Database/ExportDialogViewModel.cs` | 导出对象选择、树形勾选 | 665 |

---

## 1. 架构与分层问题

### 必须处理：DatabaseService 直接 new，未走 DI

`DatabaseService` 在多个 ViewModel 中通过 `new DatabaseService()` 直接实例化：

- `MainWindowViewModel.cs:37`
- `ConnectionDialogViewModel.cs:214`
- `TableDetailViewModel.cs:17`
- `DatabaseBrowserViewModel.cs:21`
- `ExportDialogViewModel.cs:22`

违反 `infrastructure-AGENTS.md` 中"服务类实现标记接口，通过 DI 注册"的规则。

**风险**：无法替换实现、无法 mock 测试、每个 ViewModel 持有独立实例导致连接缓存无法共享。

**建议**：将 `DatabaseService` 抽为 `IDatabaseService` 接口，注册为 `ISingletonDependency`（因内部有连接缓存），通过构造函数注入。

### 必须处理：MainWindowViewModel 职责过重

`MainWindowViewModel` 承担了连接管理、数据库切换、Schema 加载、表/视图/存储过程激活、文档导出、代码生成、连接导入导出、分组管理等至少 8 个职责，共 1687 行。违反 `application-AGENTS.md` 中"ViewModel 负责状态管理与命令触发，Service 负责业务逻辑"的分层。

**建议**：至少将文档导出、代码生成、连接导入导出抽到独立 Service。

---

## 2. 安全问题

### 必须处理：密码加密方式不安全

`MainWindowViewModel` 中 `EncryptPasswordsInPlace` / `DecryptPasswordsInPlace`（行 1661-1686）在内存中对 `ObservableCollection<ConnectionConfig>` 直接修改 Password 属性。`SaveConnections()` 中先加密再序列化，finally 中再解密。如果序列化过程中抛出异常且 finally 未执行（极端情况），密码将保持加密状态，后续连接会失败。

**风险**：密码状态不一致。

**建议**：序列化时使用临时副本而非原地修改，或使用 `JsonIgnore` + 自定义序列化。

### 建议处理：连接字符串中的密码明文

`BuildConnectionString`（行 247-265）将明文密码直接拼入连接字符串。虽然连接字符串不落盘，但在内存中以明文存在。

**当前处理**：已记录到 `doc/2026-06-02-Azrng.DataAccess-connection-security-notes.md`。该问题更适合在 `Azrng.DataAccess` 包内统一补齐 `DataSourceConfig` 构造入口、连接字符串 builder 与敏感字段脱敏能力，避免当前应用层继续散落字符串模板。

---

## 3. 代码质量问题

### 必须处理：死代码 ValidateFormLegacy / ValidateConnectionNameUniqueLegacy

`ConnectionDialogViewModel` 中存在两个 Legacy 方法，已被新版本替代但未删除：

- `ValidateFormLegacy`（行 865-924）
- `ValidateConnectionNameUniqueLegacy`（行 1031-1051）

**建议**：直接删除。

### 必须处理：MapDatabaseType 映射错误

`DatabaseService.MapDatabaseType`（行 321-333）将 `DatabaseType.Dm`（达梦）映射为 `DatabaseType.SqlServer`，将未知类型也默认映射为 `SqlServer`。

```csharp
private DatabaseType MapDatabaseType(DatabaseType type)
{
    return type switch
    {
        DatabaseType.SqlServer => DatabaseType.SqlServer,
        DatabaseType.MySql => DatabaseType.MySql,
        DatabaseType.PostgresSql => DatabaseType.PostgresSql,
        DatabaseType.Oracle => DatabaseType.Oracle,
        DatabaseType.Sqlite => DatabaseType.Sqlite,
        DatabaseType.Dm => DatabaseType.SqlServer,    // 问题：达梦不应映射为 SqlServer
        _ => DatabaseType.SqlServer                    // 问题：未知类型默认 SqlServer
    };
}
```

**风险**：达梦数据库使用 SQL Server 的桥接器和连接字符串构建逻辑，可能导致连接失败或行为异常。

**建议**：达梦应有独立的映射和桥接器，或在不支持时直接抛出 `NotSupportedException`。

### 建议处理：ShowDatabaseSelector 重复通知

`ConnectionDialogViewModel.NotifyDatabaseTypeStateChanged`（行 755-778）中 `OnPropertyChanged(nameof(ShowDatabaseSelector))` 出现了两次（行 761 和 773）。

### 建议处理：ExportDialogViewModel 中 MapExportObjectType 不完整

`MapExportObjectType`（行 644-651）只处理了 `TreeNodeType.Table`，对 View 和 StoredProcedure 直接抛异常。但导出树中可以勾选 View 和 StoredProcedure 节点。

```csharp
private static ExportObjectType MapExportObjectType(TreeNodeType nodeType)
{
    return nodeType switch
    {
        TreeNodeType.Table => ExportObjectType.Table,
        _ => throw new InvalidOperationException($"Unsupported export node type: {nodeType}")
    };
}
```

**风险**：用户选择视图或存储过程导出会抛异常。

**建议**：补充 View 和 StoredProcedure 的映射，或在 UI 层禁用非 Table 节点的勾选。

### 建议处理：MySql Schema 处理硬编码分散

`LoadSchemasAsync`（行 798-851）对 MySql 硬编码返回单个 Schema，`LoadConnectionContextAsync` 中 MySql 的 schema 用 `connection.Database` 替代。这种分支散布在多个方法中，容易遗漏。

**建议**：将 MySql 的 schema 映射策略集中到一个方法。

---

## 4. 异步与并发

### 必须处理：fire-and-forget 调用异常被吞没

多处使用 `_ = SomeAsync()` 模式，异常会被静默吞掉：

| 位置 | 调用 |
|------|------|
| `MainWindowViewModel.cs:436` | `_ = InitializeConnectionContextAsync(value)` |
| `MainWindowViewModel.cs:487` | `_ = SwitchDatabaseAsync(value)` |
| `TableDetailViewModel.cs:417` | `_ = LoadDataAsync()` |

**风险**：异步操作中的异常不会被捕获，可能导致静默失败，难以排查。

**建议**：至少用 `try/catch` 包裹并记录日志，或使用 `async void` + 全局异常处理。

### 建议处理：DatabaseService 无线程安全保护

`GetOrCreateDbBridge` 的缓存读写没有锁保护。虽然 Avalonia UI 线程通常是单线程，但如果 `TestConnectionAsync` 等方法从后台线程调用缓存，可能出现竞态。

---

## 5. 与 AGENTS.md 规则符合度

| 规则 | 状态 | 说明 |
|------|------|------|
| 服务类实现接口 + DI 注册 | 违反 | `DatabaseService` 直接 new |
| ViewModel 不直接写 SQL | 符合 | SQL 在 Service 层 |
| 异步优先 | 基本符合 | 大部分方法是 async |
| 统一结果包装 `ResultModel<T>` | 违反 | 使用元组 `(bool, T, string)` 代替 |
| 异常使用 Azrng 体系 | 违反 | 直接 `catch (Exception)` + 元组返回 |
| 禁止主构造函数 | 符合 | 未使用主构造函数 |
| 服务层不直接操作 View | 符合 | 通过 ToastService 间接 |
| 测试覆盖 | 已改善 | 已补充 Database 模块关键单元测试，本轮回归 43 个测试通过 |

---

## 6. 优先级排序

| 优先级 | 问题 | 类型 | 当前状态 |
|--------|------|------|----------|
| P0 | MapDatabaseType 映射错误（达梦 → SqlServer） | 逻辑错误 | 已处理 |
| P0 | ExportDialogViewModel MapExportObjectType 不完整 | 运行时异常 | 已处理 |
| P1 | 死代码清理（Legacy 方法） | 代码质量 | 已处理 |
| P1 | fire-and-forget 异常吞没 | 可靠性 | 已处理 |
| P2 | DatabaseService 未走 DI | 架构规范 | 已处理 |
| P2 | MainWindowViewModel 职责拆分 | 可维护性 | 需专项 |
| P2 | 文档导出与代码生成服务直接实例化 | 架构规范 | 已处理 |
| P2 | 文档导出负载构建留在主 ViewModel | 可维护性 | 已处理 |
| P2 | 代码生成负载构建留在主 ViewModel | 可维护性 | 已处理 |
| P2 | 连接配置持久化逻辑留在主 ViewModel | 可维护性 | 已处理 |
| P2 | 密码加密方式不安全 | 安全 | 已处理 |
| P2 | 连接字符串中的密码明文 | 安全 | 需包级优化 |
| P3 | 结果包装统一为 `ResultModel<T>` | 规范对齐 | 需专项 |
| P3 | ShowDatabaseSelector 重复通知 | 代码质量 | 已处理 |
| P3 | MySql Schema 处理集中化 | 可维护性 | 已处理 |
