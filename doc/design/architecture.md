---
doc_id: DES-ARCH-001
doc_type: design
module: app
feature: architecture
status: in-review
last_updated: 2026-09-12
related:
  - doc/design/index.md
  - doc/requirements/index.md
---

# 跨模块架构设计（DES-ARCH-001）

> 本文档记录跨模块的公共架构、公共数据流与公共技术决策；各模块内部设计见 `doc/design/<module>/<feature>.md`。本文为依据当前实现整理的回溯基线，待用户确认后转为 approved。

## 需求追溯

- 覆盖所有模块共用的应用框架：主窗口与导航、依赖注入、本地持久化模式、主题与设计 token、更新与发布链路。
- 各模块的业务规则不在本文重复；模块清单与业务目标见 `doc/requirements/index.md`。

## 技术栈与运行形态

- 技术栈：.NET 10、Avalonia UI 12、Semi.Avalonia、CommunityToolkit.Mvvm、Microsoft.Extensions.DependencyInjection、Azrng.Core。
- 运行形态：Windows 桌面单机应用；发布采用 Native AOT 单文件 `AzrngTools.exe`。
- AOT 约束：JSON 持久化统一走源生成序列化上下文，不使用反射序列化；依赖运行时代码生成的能力（如 XSLT 转换）在 AOT 版本不可用，由页面明确提示。

## 应用壳与导航

```text
MainWindow（主窗口）
└── MainWindowViewModel
    ├── 侧栏
    │   ├── 常用入口（按使用评分推荐，最多 3 个）
    │   ├── 分组菜单（手风琴式，支持关键字过滤，可折叠 72px / 展开 248px）
    │   └── 首页入口
    └── 内容区（页面缓存实例，经 ViewLocator 呈现）
```

- 菜单模型 `Models/MenuBar.cs`：标题、分组图标、子项列表、`MenuType`（目标 ViewModel 类型）、展开状态。菜单树在 `ViewModels/MainWindowViewModel.cs` 的 `CreateMenuBars()` 中定义，是工具目录的权威定义处。
- 页面打开：主窗口按 `MenuType` 从 DI 容器解析 ViewModel，`ViewLocator` 按 `XxxPageViewModel → XxxPageView` 命名约定创建视图；页面实例缓存复用。
- 首页 `OverviewPageView` 展示工具总数、功能分组与亮暗主题等概览信息。
- Markdown 预览页已实现但菜单未注册：`Markdown.Avalonia` 尚未完成 Avalonia 12 兼容验证，入口临时隐藏（见 README 兼容说明）。

## 模块与代码目录映射

| 业务模块 | Views | ViewModels | Services | 文档 |
| --- | --- | --- | --- | --- |
| database（数据库工作台） | `Views/Database/` | `ViewModels/Database/` | `Services/Database/` | `database/workbench` |
| network（接口调试） | `Views/Network/` | `ViewModels/Network/` | `Services/Network/` | `network/api-debug` |
| pdf（PDF 管理） | `Views/Pdf/` | `ViewModels/Pdf/` | `Services/Pdf/` | `pdf/management` |
| generation（生成与加密） | `Views/TextHandle/`、`Views/Encrypts/` | `ViewModels/TextHandle/`、`ViewModels/Encrypts/` | 无独立服务 | `generation/tools` |
| encode（编码解码） | `Views/Encode/` | `ViewModels/Encode/` | 无独立服务 | `encode/tools` |
| format（格式化） | `Views/Format/` | `ViewModels/Format/` | `Services/Format/` | `format/tools` |
| misc（其他工具） | `Views/Other/` | `ViewModels/Other/` | 根目录 `TranslationService` | `misc/tools` |
| settings（设置与更新） | `Views/Setting/` | `ViewModels/Setting/` | 根目录多个服务 | `settings/app` |
| clipboard（剪贴板，暂缓） | 未实现 | 未实现 | 未实现 | `clipboard/history` |

注意：`ViewModels/Database/MainWindowViewModel.cs` 为数据库工作台宿主 ViewModel，与主窗口 `ViewModels/MainWindowViewModel.cs` 同名不同类，引用时使用别名区分。

## 服务组织与依赖注入

- 注册约定：服务实现 `ISingletonDependency`（单例）或 `ITransientDependency`（瞬态）标记接口，由启动装配自动扫描注册；不逐个手工注册。
- 服务分三类：
  - 页面支撑：执行与解析（如 `ApiRequestExecutionService`、`PdfPageRangeParser`、`FormatConversionService`）；
  - 持久化：本地状态读写（如连接配置、使用统计、主题偏好、历史存储）；
  - 编排：跨服务流程协调（如 `ConnectionManagementCoordinator`、`ExportCoordinator`、`AppUpdateCoordinatorService`）。
- 跨 ViewModel 通知：`Utils/Events/MessageService` 消息总线（含更新状态等全局消息）。
- 日志与提示：`LoggingService` + `QueuedLogWriter` 落地日志；`ToastService` 提供全局轻提示。

## 本地持久化地图

统一模式：`%LocalAppData%\AzrngTools\` 下按功能存放 JSON 文件；写入普遍采用防抖合并落盘；文件损坏时降级处理（备份或从空重建），不阻塞应用启动。

| 数据 | 位置（约定） | 相关服务 |
| --- | --- | --- |
| 主题偏好 | `theme-settings.json` | `ThemePreferenceService` |
| 工具使用统计 | 用户目录 JSON | `ToolUsageStatsService` |
| 硬件信息缓存 | 用户目录 JSON 快照 | `HardwareInfoCacheService` |
| 接口调试历史 | 用户目录 JSON（上限 50 条） | `ApiRequestStoreService` |
| 数据库连接配置与分组 | 用户目录 JSON | `ConnectionConfigurationService` 等 |
| 剪贴板历史（暂缓，未实现） | 规划 `clipboard/history.json` | 设计见 `DES-CLIPBOARD-001` |

工具页（编码、加密、格式化、生成类）不做持久化，输入输出不落盘。

## 主题与设计 token

- 样式资源：`Styles/DesignTokens.axaml`（间距 / 圆角 / 字号 token）、`Styles/Global.axaml`（全局样式）、`Themes/*.axaml`（控件主题与主题色）。
- 约束：token 优先 `StaticResource`，主题颜色一律 `DynamicResource`；已改造页面不再新增硬编码间距、圆角、字号和颜色。
- 设计系统基线见根目录 `design-system.yaml`。

## 应用更新链路

```text
启动后台检查 / 关于页手动检查
        ↓
AppUpdateService：访问 GitHub Releases，比较数字版本（兼容旧三段/四段版本）
        ↓ 有新版本
AppUpdateCoordinatorService
  ├── 操作加信号量锁，避免并发更新
  ├── 缓存最新版本信息（本地 JSON）
  ├── 下载 AzrngTools-win-x64-portable.zip（失败记录版本，避免反复自动重试）
  └── 经 MessageService 广播更新状态
        ↓ 用户确认
ApplicationRuntimeService：退出进程 → 覆盖安装目录 → 重启
```

- 安装目录不可写时，关于页引导手动下载 Release 压缩包覆盖。

## 发布链路

- 本地：`Publish.bat` 以 Native AOT 发布单文件到 `dist/`。
- CI：GitHub Actions `release-win-x64` 工作流，推送 `main` 触发；`VERSION` 为 `auto` 时版本号 = 基准（主版本.次版本或 UTC 日期）+ `github.run_number`，严格递增；产物 `AzrngTools-win-x64-portable.zip` 并创建 GitHub Release。
- 版本与发布细节以 README 为准，本文不重复维护。

## 公共限制与兼容项

| 限制 | 影响 | 处理 |
| --- | --- | --- |
| Markdown.Avalonia 未完成 Avalonia 12 兼容验证 | Markdown 预览入口临时隐藏 | 菜单未注册，代码保留 |
| XSLT 依赖运行时代码生成 | AOT 版本 XML 转 HTML 不可用 | 页面明确提示 |
| Aspose.Pdf 为本地 DLL 引用 | 升级需手动替换 DLL 并回归 | 授权文件 `Aspose.Pdf.lic` 放应用目录 |
