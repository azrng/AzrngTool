# 任务清单（TASK）

> 本文件只维护当前仍需关注的任务与最近 5 条已完成任务。
> 规则解释与阶段门控仍以 `AGENTS.md` 为准。

---

## 活跃任务

当前无活跃任务。

---

## 最近完成

| 任务 ID | 任务名称 | 任务状态 | 最近更新时间 |
| ------- | -------- | -------- | ------------ |
| T029 | 主窗口头部还原旧版设计：新增 Styles/WindowDecorations.axaml 同键遮蔽 Ursa.Themes.Semi 2.2.0 的 KEY_URSAWINDOW_DRAWN_DECORATIONS 装饰主题（模板与样式整段拷贝，升级 Ursa 需比对），改两处——DefaultTitleBarHeight 32→52（标题栏放两行标题+副标题）、右上窗口按钮组 PART_OverlayPanel 加 8px 右边距（关闭按钮不贴窗口右缘）；标题栏改两行（标题+副标题“开发工具集 · 支持分组浏览与搜索”居左），RightContent 加回标语“放下个人素质，享受缺德人生”（主题开关左侧）；内容区首行 32→52、Toast 下移 Margin 40→60 联动；App.axaml.cs 服务扫描补应用程序集（Headless 测试进程入口为 testhost 时才能解析业务服务，生产行为不变）；新增 MainWindowHeaderRestoreTests 回归（52px 高度、两行标题层级、标语右置、装饰主题覆写生效断言），52/52 通过 | DONE | 2026-09-17 |
| T028 | 数据库工作台整体迁移至独立应用 DbTools（D:\Work\github\DbTools，经用户确认以本仓工作区版本为基线覆盖式回迁）：Models/Services/ViewModels/Views/Controls/Utils/Converters 的 Database 目录及 16 个测试移出；ToastService 上移 Services/、EqualToZero/GreaterThanZero 转换器上移 Converters/（接口调试页共用，保留）；移除 Azrng.DataAccess 包（6 种数据库驱动 + SqlClient 拖入的 Azure/MSAL/IdentityModel 认证链随之消失，预计自包含产物减重约 16.3MB）与 Avalonia.Controls.DataGrid 包（约 0.36MB，仅工作台使用，含 DataGrid.axaml 样式）；删除 ExcludeUnusedFilesFromSingleFile 剔除 target（WinForms/Drawing/Oracle/DiaSymReader 剔除对象均随数据库依赖链消失）；主导航数据库工具组、AppSubtitle/总览文案、App 资源注册同步清理；EncryptHelper 及其测试随迁。构建 0 错，测试 51/51 通过（迁移前基线 125/125）。已随 3abd860 提交并合入 main 推送 | DONE | 2026-09-17 |
| T027 | 性能问题修复第二轮：连接对话框对长生命周期集合的事件订阅泄漏（Dispose 退订）；更新包解压/删除后台化；接口调试响应读取+JSON 美化后台化、1MB 跳过美化、ContentLength 计长、响应体 1MB 截断展示、历史入库 64KB 截断并返回列表免回读、历史搜索 200ms 防抖；数据库列表加载 O(N²) 改整体替换、库搜索 250ms 防抖（间隔可注入）、schema 列表批量装载、切换连接二次校验；导出对话框按并发 4 并行拉取各 schema 表清单；工作台表/视图/存储过程列表与树节点批量 Add（TreeNodeItem.AddChildrenRange）；表详情三查询并行+过期加载序号丢弃、选 schema 三列表并行；Markdown 预览 300ms 防抖；日志改批量写+长驻 StreamWriter+10MB 轮转（QueuedLogWriter 改批量回调）；遗留同步命令后台化（JSON转C#/JSON Schema/XSLT/RSA/Hex/字数统计/JSON转义，字数统计同时改单趟扫描）；Excel 导出改直写文件流 | DONE | 2026-09-16 |
| T026 | 发布体积减半：csproj 关闭 PublishReadyToRun（R2R 原生码内联致托管 dll 膨胀约 +160MB），新增 ExcludeUnusedFilesFromSingleFile target 用 ExcludeFromSingleFile 元数据剔除 MSAL 拖入的 WinForms 运行时（约 25MB，应用零使用）、Oracle 驱动（UI 未开放新建）、DiaSymReader（仅调试用）；注释 Common.Windows.Core 包引用并停用硬件指纹 WMI 采集，硬件信息页入口从菜单注释隐藏（同 Markdown 预览页先例，恢复注释已留）；移除 DocumentFormat.OpenXml 包（PDF 管理 Word 导出改用 NPOI 自带 XWPF，FontSize 单位经实证为磅，真实 PDF 导出冒烟通过）。自包含产物 348MB→161.4MiB，启动 1.2s→2.98s（实测冒烟通过）；依赖框架产物 182MB→74.1MiB；README 体积数字同步；修复 Publish-FrameworkDependent.bat 误输出到 dist 的 bug（改回 dist-fdd）；清理 README 陈旧的 Aspose.Pdf 本地依赖章节（实际用 PdfPig） | DONE | 2026-09-15 |
| T025 | 接口调试页修复：窗口高度不足时响应结果区被请求配置卡（Body 编辑器 MinHeight=300）挤压至 0 且无滚动兜底。左列改为 Auto,*,* 星号分栏（配置卡与响应区平分剩余空间），Params/Headers/表单列表与 Body 编辑器改为内部滚动（列表包 ScrollViewer、编辑器 MinHeight=0），配置卡内网格 Auto,Auto 改 Auto,* 让 TabControl 与编辑器撑满卡片份额（修复编辑器缩成单行、卡片底部留白） | DONE | 2026-09-15 |
| T024 | 发布链路回退 JIT 自包含单文件（Publish.bat/CI 去 AOT 参数，README 与架构文档同步）并新增 AOT 暂缓恢复指南 doc/design/publish-aot.md；修复 UrsaWindow 标题栏悬浮不占布局导致的内容顶到窗口头部的遮挡，并将 MainWindow 内容改为 32px 标题底条 + 分隔线 + 内容区三行布局（标题区底色随明暗主题切换，消除与内容融为一体的漂浮感）；修复 Toast/通知贴窗口顶被裁剪（Ursa WindowToastManager 与两处 WindowNotificationManager 加 Margin 下移避开标题栏）；发布脚本拆分为自包含 Publish.bat 与依赖框架 Publish-FrameworkDependent.bat（内容改纯 ASCII 修复 UTF-8 中文被 cmd/GBK 解析拆断的问题）；窗口圆角经实验后按用户决定不做 | DONE | 2026-09-15 |

---
