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
| T036 | 关键路径性能日志埋点：新增 PerfLogScope（Utils，阈值 200ms，Dispose 时经 LocalLogHelper 写"性能检测"类日志，快路径零输出，DescribeSize 输出可读 KB/MB；与 T034 无头探针互补——探针管回归基线、埋点管实机真实输入）；埋点 14 处——格式互转/格式化（FormatConversionService，同格式去重避免双重计时）、JSON 格式化/压缩、JSON Schema 生成、SQL 格式化/压缩、正则匹配、PDF 元数据加载/分割/导出 Word（新版面分析整链路）、更新包下载与解压落位（无条件记录，服务静默更新 E2E 待发版验证的诊断依赖）、应用启动到主窗口打开（Program.StartupStopwatch 静态锚点 + MainWindow.OnWindowOpened 结算防重）；服务层 source?.Length 防空。日志落 `<exe目录>/logs/yyyyMMdd.log`，实测测试运行产生 59 条性能条目（JSON转YAML 322-338ms/393KB、Schema 509-639ms、PDF 导出 Word 首次 JIT 1136ms、SQL 207ms/13KB 均被捕获）。全解决方案构建 0 错 0 警告，测试 105/105（首轮 1 例偶发失败未复现、连续 3 轮全绿，日志中无对应 ERROR） | DONE | 2026-10-30 |
| T035 | ENV 互转 + PDF 转 Word 还原度升级（用户确认两项范围后走完阶段 0 文档与实现）：① 互转页扩 ENV——FormatLanguage 加 Env、FormatConversionService 加 ParseEnv/EnvFromJsonNode（dotenv 通行语法：整行/行尾注释需前置空白、export 前缀、单双引号、双引号转义、跨行引号值、重复键后值覆盖、${VAR} 不插值；输出按点/下标路径扁平化、按需加引号、null 输出空值、非法键 fail fast），菜单改"JSON/YAML/XML/ENV互转"，下拉由枚举驱动自动出现；修复引号值扫描下标错位与行边界降序区间判断两 bug；新增 13 个 ENV 测试。② Word 导出升级——新增 PdfPageLayoutAnalyzer（词级字号/加粗经 FontDetails、描边子路径包围盒退化识别直线段聚类有线表格≥2格、JPEG FFD8 头直通提取），ExportPdfToWord 改版面块排序写出（行内按加粗+字号分组 run、表格映射 XWPFTable 带单线边框、图片 AddPicture 按 PDF 显示尺寸 EMU），单页分析失败降级纯文本不中断；4 个 fixture 测试（纯文本回归/字号加粗/表格/图片）；REQ/DES 文档同步（format 加 ENV 规则、pdf 升级还原度并纠偏陈旧 Aspose 本地 DLL 描述为 PdfPig+NPOI 事实）。探针回归无性能劣化；全解决方案构建 0 错 0 警告，测试 105/105 通过（原 81+ENV 13+Word 4+既有 7） | DONE | 2026-10-30 |
| T034 | 实测寻找性能问题并优化：新增无头性能探针测试（AzrngTools.Tests/Performance/PerformanceProbeTests.cs，11 条热点路径：JSON 格式化/压缩/转C#/Schema、字数统计、十六进制编解码、正则分析、繁简转换、JSON转YAML、SQL格式化、PDF 拆分 120 页，预热+3 次测量取最佳，只断言功能正确性不断言耗时）；实测全部重操作均已后台化、无 UI 线程阻塞，唯一实质慢点为 JsonHelper.JsonFormatter/JsonCompress 仍走 Newtonsoft JToken——改用 JsonDocument+Utf8JsonWriter（保留重复属性名、数字原文回写、容忍注释/尾逗号与 Newtonsoft 对齐，UnescapeJsonText 异常捕获同步改 STJ JsonException），实测 1MB 格式化 175ms→33ms、压缩 192ms→31ms（5-6 倍）；其余路径基线：JSON转YAML 209ms/400KB、JSON Schema 生成 327ms/200KB、SQL 格式化 107ms/100KB（均为库主导+已后台化，不值得动）；正则 VM 已有 2 秒灾难回溯超时保护。老测试 JsonCompress 异常断言改 ThrowsAny JsonException。全解决方案构建 0 错 0 警告，测试 81/81 通过（原 70+探针 11） | DONE | 2026-10-30 |
| T033 | 性能审查与依赖升级：T027 遗留结构性项复核（CompileBindings=False 与 TableDetailContent 无虚拟化两项已随数据库工作台迁出消失；双主题项确认非纯冗余——AvaloniaEdit Fluent 主题消费 SystemAccentColor/ControlContentThemeFontFamily 等 Fluent 资源键，移除需换 AvaloniaEdit 主题+逐页视觉验证；接口调试响应体仍为 TextBox，1MB 截断已生效，换 AvaloniaEdit 待实机验证）；依赖升级 Avalonia 12.1.1→12.1.4（主工程 4 包+测试工程 2 包）、Markdown.Avalonia 12.0.0-a3→12.0.2 转正、NPOI 2.8.0→2.8.1、GTranslate 2.3.1→2.4.0、Azrng.Core 1.20.0→1.20.1、M.E.Http/Logging.Console 10.0.10→10.0.14；显式引用 System.Security.Cryptography.Pkcs/Xml 10.0.14 清除 4 条 High 传递漏洞公告（dotnet list --vulnerable 清零）；PdfSharp 评估后停留 6.2.4——6.5.0 许可证 MIT→AGPL-3.0-only 且需显式 SetAgplLicense，升级涉及 AGPL 义务待用户拍板；全解决方案构建 0 错 0 警告，测试 70/70 通过；应用代码同步阻塞调用扫描无新增问题 | DONE | 2026-10-29 |
| T032 | 健康检查整改（2026-10-28 报告代码类项）：Program.cs 补 TaskScheduler.UnobservedTaskException 兜底；ThemePreferenceService 改 temp+Move 原子写、加锁后台落盘并移出 UI 线程（新增内部构造器供测试注入目录）；AppUpdateService 解压改逐条目 ExtractArchiveWithCancellation（条目间隙响应取消+防 zip slip），两服务 TryDeleteDirectory 失败补留痕日志；ApiRequestPageViewModel.CancelRequest 捕获 ObjectDisposedException 消除 CTS 释放竞态；JsonYamlXmlPageViewModel 转换/格式化失败补日志；删测试视觉树恒空分支修 CA2021；删零引用 CommandHelper.cs。新增解压 3 项+主题持久化 3 项测试，全解决方案构建 0 错 0 警告，测试 70/70 通过（原 64） | DONE | 2026-10-28 |

---
