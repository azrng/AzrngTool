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
| T040 | 全面代码审查修复（5 路并行审查 3.5 万行后按 4 批次落地）：①小修包——FormatJson 对字面量 "null" 的 NRE 改为回退输出文本（与 Convert 一致）、Base64ToImage 三处 Bitmap 换图/清空/失败释放旧图（非托管内存泄漏）、AES 页 ILogger<DesEncryptPageViewModel> 类别错位纠正（测试 NullLogger 同步）、去除转义 \n 由整段删除改为还原换行、首页通知 Margin 40→60 对齐 52px 标题栏；②更新链路治理——新增 App.AppLifetimeCts（desktop.Exit 取消）贯穿启动检查/自动下载/关于页手动动作，下载+解压整链路 30 分钟预算（linked CTS+CancelAfter，到期转 TimeoutException 走既有失败分支并清理 staging），IsSslRelatedException 收窄为 AuthenticationException+SecureConnectionError（任意 IOException/HttpRequestException 不再触发数百 MB 整包重下）；③测试隔离——App.ConfigureServices 拆出 protected virtual 注入点、CaptureApp 覆写替换三个持久化服务为临时目录实例（MainWindowHeaderRestoreTests 不再读写真实 %LOCALAPPDATA%\AzrngTools）、新增 ModuleInitializer 统一重定向 AppLog 到 %TEMP%\AzrngTools.Tests\logs、ToolUsageStatsService 补 internal 路径 ctor、新增 global.json（10.0.100+latestFeature，本机与 CI 同 band 解析 10.0.404 GA，release.yml 无需改动）；④死代码清理——逐项 grep 验证零引用后删除 DialogHelper/FontCollection/ImageHelper/Models.Analysis 三文件/Assets 孤立样式三文件共 9 文件，MainWindow 摘除注释掉的常用入口块、OnCommonListBoxSelectionChanged 处理器与 sidebarToggleButton 死类。审查报告的「JsonHelper 转义缺失」经现有测试实机证伪（输入为 JSON 转义体时原样拼接是有意设计，加转义反而破坏主场景 2 用例），已回滚并补契约注释防再犯。全解决方案构建 0 错 0 警告（SDK 切 10.0.404 GA），测试 138/138 连续三轮全绿（首轮 1 例 AppLog 过期清理断言偶发失败，单跑/整类/后续三轮均过，属并行错误日志写入与清理断言的既有竞争，非本次引入）；遗留待拍板：P2 级修复项（PDF 页命令 CanExecute 空操作、复制命令假成功提示 12 处、ApiRequest 大响应全量缓冲、installer.nsi 过期模板等）与 ToolUsageStatsService.RecordToolUsage 零调用点的产品意图确认 | DONE | 2026-11-11 |
| T039 | PDF 转 Word 双栏阅读顺序修复：根因是 GroupIntoLines 仅按基线 y 聚类+全页按 Top 排序，双栏同基线词并成一行串行输出。改用 PdfPig 0.1.15 主包内置的 DocumentLayoutAnalysis（零新增依赖，bin 已有该 DLL）：SegmentIntoReadingOrderLines 以 RecursiveXYCut 栏感知分块 + UnsupervisedReadingOrderDetector 按栏阅读顺序，TextLine.Words 经 Dictionary<Word,PdfLayoutWord> 映射回词级字号/加粗；MergeBlocksByReadingOrder 以文本行阅读序为骨架、表格/图片按 Top 几何插入（首个 Top 更大文本行之前），不再整体 OrderBy(Top) 打散分栏序；分块失败 AppLog 记录并回退原平铺行聚类（AnalyzeTextOnly 兜底语义不变）。关键调参：RecursiveXYCutOptions.MinimumWidth 取页宽 1/10 而非官方双栏用例的 1/3——该值语义是"投影块达此宽度才允许其后切分"，官方值只覆盖半页宽栏，窄栏（侧栏 60pt 级）会被漏切回串行；切分过细无害（块被阅读序检测器重排），漏切才是致命场景。新增 CreateTwoColumnPdf 夹具与 WordExport_TwoColumnPdf_ShouldPreserveColumnReadingOrder 用例（首轮红测精确复现串行 bug，调参后转绿）。全解决方案构建 0 错 0 警告，测试 138/138 通过；真实双栏 PDF 实机抽查待用户后续验证 | DONE | 2026-11-10 |
| T038 | 页面输入记忆存档落地（阶段 0 文档 REQ/DES/CON-SETTINGS-001 经用户确认后实施，REQ/DES 转 approved、契约转 superseded）：核心 PageInputStoreService（`%LOCALAPPDATA%\AzrngTools\page-inputs\<pageId>.json` 一页一文件、Save 只更新内存快照+服务侧 800ms 防抖合并、tmp+Move 原子写、冲刷失败 TryAdd 回填、TryLoad 损坏/未知版本整页兜底、单值超 200 万字符跳过、pageId 防路径穿越、ProcessExit 限时 2s 冲刷、源生成 JSON camelCase、ISingletonDependency 自动注册+internal 目录 ctor 供测试）+ PageInputValues 标量编解码（bool/int/long/枚举序号/DateTime 往返格式，DecodeEnum 经 ToObject 再 IsDefined 避免装箱类型不符）；31 个 pageId 接入（30 个工具页 VM + MainWindowViewModel），与 DES 清单逐一相符、零缺失；敏感字段（AES/DES/SM4/HMAC Secret 与 Iv、RSA 密钥对、JWT 明文页整页不接、请求头/查询/表单/请求体、密码生成结果、CurrentFile）键不存在而非存空值；主窗口会话恢复（RestoreLastSession 在 OnWindowOpened 调用：按 MenuType.FullName 恢复上次页面+侧栏折叠，未知键保首页，_sessionSaveEnabled 门控防 ctor 初始选中覆盖存档）；实施中自审修复的关键隐患：VM ctor 默认值赋值会在恢复前提交快照、防抖落盘覆盖上次存档——10 个含 ctor 默认赋值的 VM 统一 _isRestoring 首尾包裹，ApiRequest 的 SelectedBodyModeOption→SelectedBodyMode 联动链靠值相等侥幸不触发，已显式防护；7 处 CS8601 统一 `&& x is not null` 恢复守卫；JsonSchemaPageViewModel 缺省参数删除、探针测试构造补参。新增测试 6 文件（服务 9 例/编解码 7 例/VM 恢复 3 文件 8 例），全解决方案构建 0 错 0 警告，测试 137/137 通过（headless 测试经真实 DI 解析 MainWindow 验证自动注册生效）；独立 reviewer 因配额未跑完，验收由主会话按八项清单自查完成（覆盖面比对/敏感键扫描/ctor 防护矩阵/`!` 零新增/越界检查） | DONE | 2026-11-05 |
| T037 | 性能埋点扩展 + 日志迁移应用数据目录：新增 AppLog（Utils，等价替换 Azrng.Core 的 LocalLogHelper——反射确认 1.20.1 日志根目录硬编码 exe 目录无配置入口；队列批量异步写、按天文件迁 `%LOCALAPPDATA%\AzrngTools\logs`、错误条目双写 error.log、队列上限 10000 丢最旧、30 天清理、ProcessExit 限时 2s 冲刷、全链路吞 IO 异常、目录可注入测试钩子）；主工程 39 文件 105 处调用机械改名（reviewer 逐行比对确认纯替换）；PerfLogScope 增延迟上下文重载（落日志才求值，供 API 响应规模）与 DescribeSize(string?) 重载——根因修复 `?.Length ?? 0` 判空污染可空流分析导致的 CS8604（已用最小工程复现确认），替代初版 6 处 `!` 宽恕并迁移 9 个 VM 调用点；埋点扩展 30 处——AES/DES/SM4/HMAC/RSA加解密/密钥生成/哈希、Gzip/Hex×4/Base64/Base64转图/JWT解析、XML转HTML/字数统计/JSON转义族×3/字符串压缩×2/格式校验（服务层）、JSON转C#×2、翻译、硬件采集、API 请求（方法+响应规模，性能条目不落 URL）；顺带消除基线存量 7 条警告（6×CS8604 同根因 + IPdfImage.Bounds 过时 API 换 BoundingBox）；AppLog 5 项单测（临时目录注入）。全解决方案构建 0 错 0 警告，测试 110/110 通过，reviewer 六项验收全达标（pass-with-notes 4 条 note 已消化 3 条） | DONE | 2026-11-05 |
| T036 | 关键路径性能日志埋点：新增 PerfLogScope（Utils，阈值 200ms，Dispose 时经 LocalLogHelper 写"性能检测"类日志，快路径零输出，DescribeSize 输出可读 KB/MB；与 T034 无头探针互补——探针管回归基线、埋点管实机真实输入）；埋点 14 处——格式互转/格式化（FormatConversionService，同格式去重避免双重计时）、JSON 格式化/压缩、JSON Schema 生成、SQL 格式化/压缩、正则匹配、PDF 元数据加载/分割/导出 Word（新版面分析整链路）、更新包下载与解压落位（无条件记录，服务静默更新 E2E 待发版验证的诊断依赖）、应用启动到主窗口打开（Program.StartupStopwatch 静态锚点 + MainWindow.OnWindowOpened 结算防重）；服务层 source?.Length 防空。日志落 `<exe目录>/logs/yyyyMMdd.log`（T037 起迁移至应用数据目录），实测测试运行产生 59 条性能条目（JSON转YAML 322-338ms/393KB、Schema 509-639ms、PDF 导出 Word 首次 JIT 1136ms、SQL 207ms/13KB 均被捕获）。全解决方案构建 0 错 0 警告，测试 105/105（首轮 1 例偶发失败未复现、连续 3 轮全绿，日志中无对应 ERROR） | DONE | 2026-10-30 |

---
