---
doc_id: DES-SETTINGS-002
doc_type: design
module: settings
feature: page-input-archive
status: approved
last_updated: 2026-11-05
related:
  - doc/requirements/settings/page-input-archive.md
  - doc/contracts/settings/page-input-archive.md
  - doc/design/settings/app.md
  - doc/design/architecture.md
---

# 页面输入记忆存档设计（DES-SETTINGS-002）

## 需求追溯

- 关联需求：`REQ-SETTINGS-002`（draft，待确认）。
- 覆盖：页面输入快照存储服务、约 24 个工具页与主窗口的接入、敏感字段排除。
- 不覆盖：日志位置迁移（T037 已完成）、既有工具使用统计与接口调试历史、设置页开关界面。

## 契约引用

- 存档文件结构：`CON-SETTINGS-001`（draft，见 `doc/contracts/settings/page-input-archive.md`）；实现落地后以 `Services/PageInputStoreService.cs` 为最终事实来源。

## 技术设计

### 版本事实

- .NET 10（`net10.0-windows`）、Avalonia 12.1.4、CommunityToolkit.Mvvm（版本以仓库 `AzrngTools.csproj` 锁定为准）；核实日期 2026-11-05。
- 发布链路为自包含单文件（AOT 暂缓，见 `DES-PUBLISH-001`/`publish-aot.md`），但序列化仍按源生成实现，保持对裁剪的兼容，不用反射序列化。

### 模块职责

| 模块 | 生命周期 | 职责 |
| --- | --- | --- |
| `IPageInputStoreService` / `PageInputStoreService` | Singleton | 页面输入快照的保存（服务侧防抖、原子写、截断保护）与恢复（同步读取）；退出时冲刷待写页 |
| 各页面 ViewModel | 页面级 | 声明本页 `pageId` 与记忆键集合：构造时恢复、属性变更时提交快照 |
| `MainWindowViewModel` | Singleton | 记上次打开页面与侧栏折叠；启动后按存档导航到上次页面 |

服务用 `ISingletonDependency` 标记接口注册（与 `ApiRequestStoreService` 同模式）；构造函数保留 `internal PageInputStoreService(string directory)` 供测试注入临时目录。

### 数据流与状态变化

- 保存路径：VM 属性变更（`OnXxxChanged`）→ 组装本页全量键值字典 → `SaveAsync(pageId, dict)` 立即返回 → 服务内按 pageId 记录最新快照并合并到 800ms 防抖 → 到期后写 `page-inputs/<pageId>.json`（`.tmp` + `File.Move` 原子替换）。连续键入被防抖合并为一次磁盘写。
- 恢复路径：VM 构造函数（页面实例经 `_pageCache` 缓存，仅首次创建时执行）→ `TryLoad(pageId)` 同步读文件（小文件，毫秒级）→ 逐键显式赋值回属性。
- 防抖在服务侧而非 VM 侧：VM 每次变更只做字典组装，延迟与合并逻辑集中一处；进程退出时服务挂 `AppDomain.ProcessExit`，限时 2 秒把全部待写快照同步落盘。
- 还原回写防护：VM 恢复赋值会触发 `OnXxxChanged`，用 `_isRestoring` 标志跳过恢复期间的保存提交；防抖到期时组装的是完整当前状态，即使中途触发一次也只多写一次正确快照。

### 关键技术决策

1. **键值字典而非每页强类型快照**：`Dictionary<string, string?>`，标量统一转字符串（`bool`→`"true"`、数值→不变性文化字符串、枚举→序号字符串）。每页新增记忆键只加一行，旧存档缺键走默认值，天然向前兼容；源生成序列化只需注册一个快照类型，避免 30 个类型登记。页面代码直白可读，不引入反射/表达式树抽象。
2. **整页单文件**：页粒度防抖写，页之间互不影响；单页损坏只丢一页。
3. **敏感字段排除靠"不写键"实现**：该页组装字典时直接不含敏感键（如 `Secret`），存档文件中不存在该键，而非写空值。
4. **截断保护**：单值超 2,000,000 字符跳过该键并经 `AppLog` 记录（实测最大输入为 JSON 转 YAML 的 393KB，上限留 5 倍余量）。
5. **损坏兜底**：读文件任何异常（损坏、JSON 解析失败）→ 返回空字典 + `LogError`，页面走默认值；写失败静默重试到下一轮防抖，持续失败不影响功能。

### pageId 与接入清单

pageId 命名 `<分组>-<页面>` kebab-case，与文件名一致；键名用属性名原样（`nameof`）。

| pageId | 保存键 | 排除键 |
| --- | --- | --- |
| encode-base64 | Original | — |
| encode-base64-to-image | Base64String | — |
| encode-chinese-convert | Original | — |
| encode-workbench | OriginalText | — |
| encode-gzip | OriginalText | — |
| encode-hex | Original; EncodeFormat; SeparatorIndex | — |
| encode-unicode | Original | — |
| encode-url | Original | — |
| encrypts-aes | Original; InputOriginalTypeIndex; SecretTypeIndex; OutTypeIndex; CipherModeIndex; PaddingModeIndex | Secret; Iv |
| encrypts-des | Original; OutTypeValue | Secret |
| encrypts-hash | Original; OutTypeValue | — |
| encrypts-hmac | Original; OutTypeValue | Secret |
| encrypts-rsa | Original; RsaKeyTypeValue; OutTypeValue | PublicKey; PrivateKey |
| encrypts-sm4 | Original; OutTypeValue | Secret |
| format-json | Original | — |
| format-json-yaml-xml | SourceText; SourceFormat; TargetFormat | — |
| format-mime-query | SearchText | — |
| format-regex | TestText; RegexPattern; ReplacementText; IgnoreCase; Multiline; Singleline | — |
| format-sql | OriginText | — |
| format-string | Original | — |
| format-word-count | InputText | — |
| format-xml-to-html | XmlContent; XsltContent | — |
| generation-json-schema | OriginText | — |
| generation-json-to-csharp | JsonInput; RootClassName; NameSpace; UseNullableTypes; UsePascalCase; GenerateProperties | — |
| generation-password | PasswordLength; BatchCount; IncludeNumbers; IncludeLowercase; IncludeUppercase; IncludeSpecialChars | 生成结果 |
| network-api-request | RequestUrl; SelectedMethod; SelectedBodyMode; IgnoreSslErrors; SelectedConfigTabIndex | RequestBody; Headers/Query/Form 的 Name/Value 集合 |
| other-translator | OriginText; TranslatorTypeIndex | — |
| other-unix-timestamp | TimestampSeconds; Milliseconds; TargetDateTime; TargetDateSelection; TargetTimeSelection | — |
| pdf-management | PageRangeText; IsIndividualPagesMode; SplitOutputPath; WordOutputPath | CurrentFile |
| texthandle-markdown | MdText | — |
| main-window | SelectedListItem（页面标识）; IsSidebarCollapsed | SearchKeyword |

明确不接入：JWT 解析页（唯一输入为令牌明文）、人民币大写、GUID 生成、关于页、硬件页。

### 依赖边界

- ViewModel 只依赖 `IPageInputStoreService`，不直接触碰文件与序列化；新增 ctor 参数由既有 DI 解析（页面 VM 已由容器创建，`ApiRequestPageViewModel` 注入服务为同一模式）。
- 不修改 `ToolUsageStatsService` 的无退出冲刷缺口（发现但不在本任务处理）。

## 界面原型

无界面变更：所有行为为页面加载时的自动恢复与后台保存，无新增控件、状态视图或交互入口，线框图不适用。

## 交互与异常

### 操作流程与状态流转

1. 启动 → 主窗口读取 `main-window` 存档 → 恢复侧栏折叠状态 → 导航到上次页面；
2. 页面首次创建 → `TryLoad` → 有存档则逐键恢复（`_isRestoring` 期间不提交保存）→ 恢复完成；
3. 用户编辑 → `OnXxxChanged` → 组装全量快照 → `SaveAsync`（合并进防抖）→ 防抖到期原子写盘；
4. 退出 → ProcessExit → 服务限时同步冲刷全部待写页。

### 边界场景与错误处理

| 场景 | 处理 |
| --- | --- |
| 存档文件不存在 | 返回空字典，页面用默认值，无日志噪音 |
| 存档损坏 / JSON 解析失败 | 整页丢弃返回空字典 + `LogError`，不影响页面与其他页 |
| 单值超 2,000,000 字符 | 跳过该键 + `LogError`，其余键正常保存 |
| 写盘 IO 失败（文件占用等） | 保留待写快照等下一轮防抖重试，异常不外抛 |
| 进程退出时有未落盘快照 | ProcessExit 限时 2 秒同步冲刷；超时放弃（丢失最后不足一秒变更，可接受） |
| `SaveAsync` 的未观察异常 | 服务内部全量捕获记日志，调用方 fire-and-forget 安全 |

### 验证方式

- 单元测试（仿 `ThemePreferenceServiceTests` 临时目录模式）：保存-恢复往返、损坏兜底、超限跳过、原子写无 `.tmp` 残留、退出冲刷（暴露 `internal FlushAllForTests`）；
- ViewModel 接入示例测试（如 JSON 页）：临时目录服务实例下，设置属性 → 冲刷 → 新建 VM 实例断言恢复；加密页断言存档中不存在 `Secret` 键；
- 手测清单：对照接入清单逐页改输入重启验证恢复（抽测 ≥5 页含主窗口）；
- 提交前门禁：全解决方案 `dotnet build` 0 错 0 警告，`dotnet test` 全绿。
