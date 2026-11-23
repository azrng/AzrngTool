---
doc_id: DES-PDF-002
doc_type: design
module: pdf
feature: to-markdown
status: in-review
last_updated: 2026-11-23
related:
  - doc/requirements/pdf/to-markdown.md
  - doc/design/pdf/management.md
  - doc/design/word/to-markdown.md
---

# PDF 导出 Markdown 设计（DES-PDF-002）

> 依据 2026-11-23 调研（三真实样本探针实测，样本含 arXiv 双栏论文、ctex 中文 LaTeX 手册、Edge 网页打印文档）与用户拍板整理：技术路线自建（复用 `PdfPageLayoutAnalyzer` 版面块，零新增依赖），入口并入 PDF 管理页，标题三级判定。

## 需求追溯

- 关联需求：`REQ-PDF-002`。
- 本次覆盖：MD 导出命令、三级标题判定、加粗/斜体/超链接/有线表格还原、图片双模式导出、图片型 PDF 拒绝。
- 不覆盖：md 预览区、OCR、无线/三线表、批量、加密 PDF 解密。

## 契约引用

- 请求 / 结果模型以 `Models/Pdf/` 下类型为最终事实来源（与 `DES-PDF-001` 同口径，本模块无跨模块契约，不立 CON- 编号，以代码为准）。

## 技术设计

### 模块职责

| 模块 | 职责 |
| --- | --- |
| `PdfManagementPageViewModel` | 页面编排增量：MD 输出路径与图片模式状态、选择输出 / 导出 Markdown 命令、输入记忆存档扩展（复用既有文件载入与忙态链路） |
| `AsposePdfProcessingService` | 新增 `ExportMarkdownAsync`：复用既有 PDF 打开与逐页循环骨架，图片型判定，逐页取版面块交映射器组装，落盘与错误包装 |
| `PdfMarkdownComposer`（新增，`Services/Pdf/MarkdownExport/`） | 纯映射层：版面块 → GFM 文本（标题三级判定、加粗/斜体 run 分组、链接映射、GFM 表格、段落合并、图片引用）；不直接依赖 UI 与文件系统 |
| `PdfPageLayoutAnalyzer`（既有，微扩） | `PdfLayoutWord` 增加 `IsItalic`；`PdfImageBlock` 增加图片格式字段并扩展 `ExtractImages` 支持非 JPEG 图片经 `IPdfImage.TryGetPng` 解码为 PNG（0.1.16 公开 API） |

### 数据流向

```text
（既有）选择本地 PDF → 载入摘要（页数/大小）
        ↓ 输出路径 + 图片模式确认
AsposePdfProcessingService.ExportMarkdownAsync(request)
  ├── 图片型判定（全文档平均每页字母数 < 阈值）→ 失败结果「图片型/扫描件不支持」，流程终止
  ├── 读取大纲书签 → (页码, 层级, 纵坐标) 映射
  └── 逐页：
        ├── PdfPageLayoutAnalyzer.Analyze(page)（单页失败降级纯文本，与导出 Word 一致）
        ├── Page.GetHyperlinks() / GetMarkedContents()
        └── PdfMarkdownComposer：标题判定 → run 分组 → 段落合并 → 表格/图片块映射
              ↓
        写出 .md + 统计（标题数/图片数）
              ↓
        结果反馈（成功含统计 / 失败原因），无预览区
```

### 关键技术决策

1. **自建映射管线，零新增依赖**（2026-11-23 调研定选）：转换所需的双栏阅读序文本行、词级字号/加粗、有线表格网格、图片提取均已在 `PdfPageLayoutAnalyzer` 落地并经 T039/T041 验证；第三方库逐一排查均不适配（MarkItDown 移植版依赖云 SDK 全家桶或纯启发式、DoclingDotNet 30MB 原生依赖、DocSharp 无 PDF 包），详见调研记录。
2. **标题三级判定**（优先级从高到低，命中即停）：
   - **书签坐标锚定**：`PdfDocument.TryGetBookmarks` 取 `DocumentBookmarkNode.PageNumber` + `Destination.Coordinates.Top`。书签 Top 是 PDF 用户空间自页底 y 且指向标题行顶边，需先转距页顶口径（`页高 − Top`）再与行顶边（行 Top − 行高）比较，容差 8pt；层级 = 节点层级 + 1。**锚定为贪心唯一配对**：每书签只锚定距离最近的一行、每行最多一个书签——直接按行匹配会在密集排版页（版本历史/索引，同基线行块被切分）把同基线全部行误标为标题（真实样本实测 142 行误报）。端到端实测：arXiv 双栏论文章节层级全部正确、零正文误报。
   - **结构标签**：`Page.GetMarkedContents()` 取非 artifact 的 H1~H6 标签元素，递归聚合并入后代字母后经行首词首字母引用匹配归属（PdfPig 的 `AddLetter` 只写最内层 BMC，仅用元素自带 Letters 会漏配）。已知边界：Chromium 网页打印 PDF 的 H 标签为空 BMC 占位（letters=0，正文在兄弟 NonStruct 元素中），标签路径对其无效。
   - **视觉字号兜底（强约束）**：整行平均字号 ≥ 正文众数字号 × 1.35，且行文本 2~60 字符、1~12 个词、非纯数字/纯符号、行内词字号均匀（最大最小差 ≤ 30%）；≥1.7 倍出 `#`、≥1.5 出 `##`、否则 `###`。约束清单用于压制探针实测的行号/上标/装饰字符误报。
3. **图片型 PDF 判定**：转换前统计全文档 `Page.Letters` 总数，平均每页 < 10 字符判定为图片型，返回失败「该 PDF 无可提取文本（图片型/扫描件内容），暂不支持转换为 Markdown」。阈值为实现期可调常量，含搜索型扫描件（有 OCR 文字层）不受影响——其有文字层，正常转换。
4. **图片提取扩展（影响链同步）**：`PdfImageBlock` 由 `JpegBytes` 改为 `Bytes` + `Format`（JPEG/PNG）；`ExtractImages` 对非 JPEG 图片走 `IPdfImage.TryGetPng`（实测 arXiv 3 张全部成功；JPX/CCITT 仍不可解跳过并写日志）。影响链：构造点 `ExtractImages`、消费点 `WriteImage`（按 Format 映射 NPOI `PictureType`）——导出 Word 的图片支持面随之变宽（原仅 JPEG），`PdfLayoutWord`/`PdfImageBlock` 全部引用仅 `AsposePdfProcessingService.cs` 与 `PdfPageLayoutAnalyzer.cs` 两文件（已 grep 核实，测试零直接引用）。
5. **加粗/斜体与链接**：行内相邻词按（加粗、斜体、链接目标）分组为 run，输出 `**粗**` / `*斜*` / `[文本](地址)`；链接经 `Page.GetHyperlinks()` 词中心点命中 `Bounds` 矩形映射，URI 含空格/括号时用尖括号包裹。导出 Word 链路本次不动斜体（范围外不顺手改），仅 MD 消费 `IsItalic`。
6. **表格与段落**：有线表格网格 → GFM（首行表头 + 分隔行，`|` 转义、单元格内多行合并空格）；相邻文本行行距 ≤ 行高 × 0.9 判同段合并，中文相邻字符间不加空格（探针验证）。
7. **结果包装**：对齐 PDF 域约定，`PdfResultModel` + `BuildFriendlyError` 分层，Service 不抛异常；无预览区，成功消息含输出路径 + 标题数 + 图片数（与分割 / 导出 Word 反馈口径一致）。
8. **输入记忆**：`pageId=pdf-management` 存档新增 `MarkdownOutputPath`、`ExportMdImages` 两键（对齐既有四键口径）；`CurrentFile` 不入存档；新命令 CanExecute 接 `CanOperate` 真条件（沿用 T041 修复后的刷新链路）。落地时核对 `doc/design/settings/page-input-archive.md` 接入清单是否需同步。

### 状态变化

- `MarkdownOutputPath`：载入成功时默认 `源文件同名.md`，用户可改，入存档；
- `ExportMdImages`：默认 true（导出），入存档；
- `IsBusy` / `BusyText` / `StatusText` / `LastResultText`：复用既有忙态与反馈链路；
- 清空命令：`MarkdownOutputPath` 随既有字段一并清空。

## 界面原型

PDF 管理页右列由三卡扩为四卡（沉淀型真栈页面直接在 `PdfManagementPageView.axaml` 修改，不另立原型工程）：

```text
左列（不变）                          右列
┌──────────────────────┐  ┌──────────────────────────────┐
│ PDF 文件              │  │ 分割 PDF                      │
│ 页码范围              │  ├──────────────────────────────┤
│ 处理状态              │  │ 导出 Word                     │
└──────────────────────┘  ├──────────────────────────────┤
                          │ 导出 Markdown（新增）          │
                          │ 输出路径 [___________] [选择]  │
                          │ [x] 导出图片到 .assets 目录    │
                          │ [开始转换]                     │
                          ├──────────────────────────────┤
                          │ 快捷操作                      │
                          └──────────────────────────────┘
loading：复用左列处理状态进度条；error：处理状态区与消息提示；
```

## 交互与异常

| 场景 | 处理 |
| --- | --- |
| 图片型 PDF | 转换前判定，提示「无可提取文本（图片型/扫描件内容）」，不产生输出文件 |
| 输出路径为空 | 提示「请选择 Markdown 输出文件」，不进入转换 |
| 源文件损坏 / 加密 | 与导出 Word 一致，失败结果 + 可读原因 |
| 转换中 | 复用既有忙态（进度条 + BusyText），不阻塞界面 |
| 单页版面分析 / 图片解码异常 | 单页降级为纯文本并写日志，导出整体不失败（与导出 Word 同口径） |
| 转换成功 | 提示输出路径 + 标题数 + 图片数 |

## 验证方式

- 单元测试（`AzrngTools.Tests/Services/Pdf/MarkdownExport/`）：
  - `PdfMarkdownComposer` 纯函数用例：字号兜底约束（命中/行号误报压制）、表格转义与多行合并、段落合并（中文无空格）、链接矩形命中映射、书签坐标匹配；
  - fixture 用例（复用 `WordExportFixtureBuilder` 造 PDF）：标题（字号路径）/ 加粗 / 表格 / JPEG+PNG 图片 → `ExportMarkdownAsync` → 断言 md 结构与 assets 目录文件；全图片页 fixture → 断言图片型失败消息且无输出文件。
- 影响链回归：导出 Word 既有用例全绿（`PdfImageBlock.Format` 扩展后 PNG 图片开始被 Word 导出，相关断言核对）。
- 构建：解决方案 0 错 0 警告；全量测试通过（基线既有失败按对照口径处理）。
- 端到端：调研样本三份（arXiv 双栏 / ctex 中文手册 / Edge 网页打印）实机转换，核对标题层级、链接与图片相对引用；真实中文公文档待用户后续验证。
