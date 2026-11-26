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
| T046 | 修复 PDF 管理页上传报 “the loaded license is not valid”：根因是 aa9cdd4 批量升级把 PdfSharp 6.2.4→6.5.0（AGPL-3.0-only，构造 PdfDocument 前须显式 SetAgplLicense），测试 fixture 已接受而主程序未调，运行时上传/分割在 PdfReader.Open 抛 PdfSharpLicenseException。经用户拍板接受 AGPL 义务保留 6.5.0：Program.Main 启动即调 PdfSharpLicense.SetAgplLicense、csproj 注释同步义务口径（原“停在 6.2.4 待拍板”作废）、AsposePdfProcessingServiceTests.CreateSamplePdf 补授权调用（该用例直建 PDF 未走 fixture，全量测试曾因此 1 例失败）；全解决方案 0 错 0 警、174/174 通过；AGPL 义务提示：对外分发（含网络服务）须公开整个应用源码 | DONE | 2026-11-26 |
| T044 | PDF 管理页新增「导出 Markdown」（REQ/DES-PDF-002）：自建映射管线零新依赖——PdfPageLayoutAnalyzer 版面块 → 新增 PdfMarkdownComposer（GFM 纯映射）。标题三级判定：书签坐标（PdfPig Top 为自页底 y 且指向行顶边，须 页高−Top 后与 行Top−行高 匹配，容差 8pt，贪心唯一配对防密集页同基线行全误标——实测修复 647→228 误报并保 arXiv 章节层级全对）→ 结构标签（H1~H6 递归聚合后代字母，PdfPig AddLetter 只写最内层 BMC；Chromium 空 H3 占位为已知边界）→ 字号兜底强约束（≥正文众数1.35 倍+2~60字+≤12词+非纯符号+行内字号均匀）。图片型判定平均每页字母<10 拒绝；PdfImageBlock 加 Format 扩 TryGetPng（PNG 图片 Word/MD 双导出）；VM 增 MarkdownOutputPath/ExportMdImages 存档+双命令+页面第三功能区卡；fixture 补 SetAgplLicense（6.5.0 需显式接受，连带恢复基线 7 个 AGPL 失败用例）+CreatePng/ImageOnlyPdf。26 新用例；全解决方案 0 错 0 警、173/173 通过；三真实样本端到端验证（arXiv/ctex/learn-zh），已知边界见 tasks/T044.md | DONE | 2026-11-23 |
| T043 | README 按 doc-AGENTS「项目 README 维护」收敛最简版：删样式/发布/版本细节（design-system.yaml、architecture.md 为权威），保留简介/技术栈/功能/兼容/环境/构建运行/版本发布最小段/结构/文档索引；同步 publish-aot.md 小节引用；构建命令已验证 | DONE | 2026-11-23 |
| T042 | Word 转 Markdown：新增「文档工具 · Word转Markdown」页（REQ/DES-WORD-001）。选型实测定 DocSharp.Docx 0.21.0（MIT，+0.93MB，唯一按 styles.xml w:name 解析标题的候选，中文 Word 本地化数字 styleId 正确映射 # 层级；openxml-markdown/OfficeIMO 真实文档标题全丢、pandoc 41MB 外部进程落选）。服务层双设置产相对图片链接（.assets 目录导出/忽略双模式）+ _Toc 锚点清洗；坑位：Models.Word 命名空间遮蔽 PdfPig Content.Word（别名修复）、NPOI 裸文档不写 styles.xml（fixture 显式补 heading 定义）、DocSharp 无图不建 assets 目录（计数前 Exists 判断）。9 新用例全绿；端到端真实接口文档 984KB→12 标题/11 图全落盘相对引用约 300ms；全量 147=140 过+7 失败为基线既有（HEAD aa9cdd4 PdfSharp 6.5.0 AGPL 异常，worktree 基线对照确认与本任务无关） | DONE | 2026-11-23 |
| T041 | T040 遗留 P2 批次落地：①PDF 页命令 CanExecute 空操作——根因是 [RelayCommand] 生成的命令 CanExecute 恒 true、NotifyCommandStates 六处 NotifyCanExecuteChanged 全部空转，修复为接真条件（ChoosePdfFile/Clear→CanExecuteWhileIdle=!IsBusy，ChooseSplitOutput/ChooseWordOutput/SplitPdf/ExportWord→CanOperate=HasFile&&!IsBusy，OnCurrentFileChanged/OnIsBusyChanged 既有刷新链路随之生效；命令体内 guard 保留作程序化调用防御）；Avalonia 12.1.4 源码核实 Button.IsEnabledCore=IsEnabled&&_commandCanExecute，命令通道与 axaml 显式 IsEnabled 绑定相与共存不冲突，绑定按最小扰动保留。②复制假成功 19 处——ClipboardHelper.SetTextAsync 改签 Task<bool>（TopLevel/Clipboard 为 null 返回 false；Avalonia 12 写侧无 Try API，剪贴板被占用时 SetTextAsync 抛异常，捕获后返回 false），19 处调用点按返回值提示"已复制/复制失败：剪贴板不可用"，ApiRequest 响应体/响应头两处顺带补齐缺失的 try/catch。③ApiRequest 大响应全量缓冲——client.SendAsync 改 HttpCompletionOption.ResponseHeadersRead（默认模式在返回前已整包进内存）+ 新增 ReadBodyWithCapAsync 流式读取上限 4M 字符（取 VM 展示截断阈值 1M 的 4 倍保证 UI 截断提示语义不变，超限即弃流），DurationMs 结算移至读完响应体（防 ResponseHeadersRead 下退化为 TTFB），截断点剥离孤立 UTF-16 高代理项；一次性冒烟工程实测 50MB 响应进程分配 16MB（原需 100MB+）、SizeBytes 仍取 Content-Length。④删除 installer.nsi——NSIS 官方模板残留（引用 AvaloniaAOT 旧原型路径，全仓零构建/源码引用）。⑤ToolUsageStatsService.RecordToolUsage 零调用点——核实为有意搁置非漏接线：4da6ef9"暂时禁用常用入口"随引用同步 bug 未解将 UI 与调用点一并注释（OnSelectedListItemChanged 内注释保留 _lastRecordedMenuKey 去重接线），服务、评分与 GetTopToolKeys 消费链完整，恢复接线与否待产品拍板。全解决方案构建 0 错 0 警告，测试 138/138 通过 | DONE | 2026-11-11 |
| T040 | 全面代码审查修复（5 路并行审查 3.5 万行后按 4 批次落地）：①小修包——FormatJson 对字面量 "null" 的 NRE 改为回退输出文本（与 Convert 一致）、Base64ToImage 三处 Bitmap 换图/清空/失败释放旧图（非托管内存泄漏）、AES 页 ILogger<DesEncryptPageViewModel> 类别错位纠正（测试 NullLogger 同步）、去除转义 \n 由整段删除改为还原换行、首页通知 Margin 40→60 对齐 52px 标题栏；②更新链路治理——新增 App.AppLifetimeCts（desktop.Exit 取消）贯穿启动检查/自动下载/关于页手动动作，下载+解压整链路 30 分钟预算（linked CTS+CancelAfter，到期转 TimeoutException 走既有失败分支并清理 staging），IsSslRelatedException 收窄为 AuthenticationException+SecureConnectionError（任意 IOException/HttpRequestException 不再触发数百 MB 整包重下）；③测试隔离——App.ConfigureServices 拆出 protected virtual 注入点、CaptureApp 覆写替换三个持久化服务为临时目录实例（MainWindowHeaderRestoreTests 不再读写真实 %LOCALAPPDATA%\AzrngTools）、新增 ModuleInitializer 统一重定向 AppLog 到 %TEMP%\AzrngTools.Tests\logs、ToolUsageStatsService 补 internal 路径 ctor、新增 global.json（10.0.100+latestFeature，本机与 CI 同 band 解析 10.0.404 GA，release.yml 无需改动）；④死代码清理——逐项 grep 验证零引用后删除 DialogHelper/FontCollection/ImageHelper/Models.Analysis 三文件/Assets 孤立样式三文件共 9 文件，MainWindow 摘除注释掉的常用入口块、OnCommonListBoxSelectionChanged 处理器与 sidebarToggleButton 死类。审查报告的「JsonHelper 转义缺失」经现有测试实机证伪（输入为 JSON 转义体时原样拼接是有意设计，加转义反而破坏主场景 2 用例），已回滚并补契约注释防再犯。全解决方案构建 0 错 0 警告（SDK 切 10.0.404 GA），测试 138/138 连续三轮全绿（首轮 1 例 AppLog 过期清理断言偶发失败，单跑/整类/后续三轮均过，属并行错误日志写入与清理断言的既有竞争，非本次引入）；遗留待拍板：P2 级修复项（PDF 页命令 CanExecute 空操作、复制命令假成功提示 12 处、ApiRequest 大响应全量缓冲、installer.nsi 过期模板等）与 ToolUsageStatsService.RecordToolUsage 零调用点的产品意图确认 | DONE | 2026-11-11 |

---
