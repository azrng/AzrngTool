# 任务清单（TASK）

> 本文件只维护当前仍需关注的任务与最近 5 条已完成任务。
> 规则解释与阶段门控仍以 `AGENTS.md` 为准。

---

## 活跃任务

| 任务 ID | 任务名称 | 任务状态 | 最近更新时间 |
| ------- | -------- | -------- | ------------ |
| — | （无活跃任务） | — | — |

---

## 最近完成

| 任务 ID | 任务名称 | 任务状态 | 最近更新时间 |
| ------- | -------- | -------- | ------------ |
| T032 | 健康检查整改（2026-10-28 报告代码类项）：Program.cs 补 TaskScheduler.UnobservedTaskException 兜底；ThemePreferenceService 改 temp+Move 原子写、加锁后台落盘并移出 UI 线程（新增内部构造器供测试注入目录）；AppUpdateService 解压改逐条目 ExtractArchiveWithCancellation（条目间隙响应取消+防 zip slip），两服务 TryDeleteDirectory 失败补留痕日志；ApiRequestPageViewModel.CancelRequest 捕获 ObjectDisposedException 消除 CTS 释放竞态；JsonYamlXmlPageViewModel 转换/格式化失败补日志；删测试视觉树恒空分支修 CA2021；删零引用 CommandHelper.cs。新增解压 3 项+主题持久化 3 项测试，全解决方案构建 0 错 0 警告，测试 70/70 通过（原 64） | DONE | 2026-10-28 |
| T031 | 应用壳原型与标题栏美化：按用户选型（Avalonia 最小工程、还原现状）新建 doc/design/prototype/app/shell-proto 原型（UrsaWindow 装饰标题栏两行标题、侧栏分组导航收展、统计卡片、主题切换，纯模拟数据）；原型确认后回写主工程——标题底条纯色改 TitleBar.StripGradient 主题渐变（明 #DCEBFF→#F7F8FA、暗 #1F2B45→#1C1C1F，ThemeDictionaries 分主题），新增 3×36 品牌竖条（Brush.Accent.Strong），标题左距 56px + 顶补正 6px（实测文字 y 8-43 上下留白平衡），右侧标语改胶囊徽章（淡蓝底+品牌描边，12px SemiBold）。验证：全解决方案构建 0 错、测试 64/64 通过，明暗两主题实测截图像素采样全部命中令牌（深色渐变顶 (31,42,66)、竖条 (127,179,255)）；design-system.yaml layout.shell.title_bar 登记全部取值置"已定制"，architecture.md 与原型 README 互链 | DONE | 2026-10-18 |
| T028 | 数据库工作台整体迁移至独立应用 DbTools（D:\Work\github\DbTools，经用户确认以本仓工作区版本为基线覆盖式回迁）：Models/Services/ViewModels/Views/Controls/Utils/Converters 的 Database 目录及 16 个测试移出；ToastService 上移 Services/、EqualToZero/GreaterThanZero 转换器上移 Converters/（接口调试页共用，保留）；移除 Azrng.DataAccess 包（6 种数据库驱动 + SqlClient 拖入的 Azure/MSAL/IdentityModel 认证链随之消失，预计自包含产物减重约 16.3MB）与 Avalonia.Controls.DataGrid 包（约 0.36MB，仅工作台使用，含 DataGrid.axaml 样式）；删除 ExcludeUnusedFilesFromSingleFile 剔除 target（WinForms/Drawing/Oracle/DiaSymReader 剔除对象均随数据库依赖链消失）；主导航数据库工具组、AppSubtitle/总览文案、App 资源注册同步清理；EncryptHelper 及其测试随迁。构建 0 错，测试 51/51 通过（迁移前基线 125/125）。已随 3abd860 提交并合入 main 推送 | DONE | 2026-09-17 |
| T027 | 性能问题修复第二轮：连接对话框对长生命周期集合的事件订阅泄漏（Dispose 退订）；更新包解压/删除后台化；接口调试响应读取+JSON 美化后台化、1MB 跳过美化、ContentLength 计长、响应体 1MB 截断展示、历史入库 64KB 截断并返回列表免回读、历史搜索 200ms 防抖；数据库列表加载 O(N²) 改整体替换、库搜索 250ms 防抖（间隔可注入）、schema 列表批量装载、切换连接二次校验；导出对话框按并发 4 并行拉取各 schema 表清单；工作台表/视图/存储过程列表与树节点批量 Add（TreeNodeItem.AddChildrenRange）；表详情三查询并行+过期加载序号丢弃、选 schema 三列表并行；Markdown 预览 300ms 防抖；日志改批量写+长驻 StreamWriter+10MB 轮转（QueuedLogWriter 改批量回调）；遗留同步命令后台化（JSON转C#/JSON Schema/XSLT/RSA/Hex/字数统计/JSON转义，字数统计同时改单趟扫描）；Excel 导出改直写文件流 | DONE | 2026-09-16 |
| T026 | 发布体积减半：csproj 关闭 PublishReadyToRun（R2R 原生码内联致托管 dll 膨胀约 +160MB），新增 ExcludeUnusedFilesFromSingleFile target 用 ExcludeFromSingleFile 元数据剔除 MSAL 拖入的 WinForms 运行时（约 25MB，应用零使用）、Oracle 驱动（UI 未开放新建）、DiaSymReader（仅调试用）；注释 Common.Windows.Core 包引用并停用硬件指纹 WMI 采集，硬件信息页入口从菜单注释隐藏（同 Markdown 预览页先例，恢复注释已留）；移除 DocumentFormat.OpenXml 包（PDF 管理 Word 导出改用 NPOI 自带 XWPF，FontSize 单位经实证为磅，真实 PDF 导出冒烟通过）。自包含产物 348MB→161.4MiB，启动 1.2s→2.98s（实测冒烟通过）；依赖框架产物 182MB→74.1MiB；README 体积数字同步；修复 Publish-FrameworkDependent.bat 误输出到 dist 的 bug（改回 dist-fdd）；清理 README 陈旧的 Aspose.Pdf 本地依赖章节（实际用 PdfPig） | DONE | 2026-09-15 |

---
