# 2026-11-23 Word 转 Markdown

## 任务
T042，REQ/DES-WORD-001：新增「文档工具 · Word转Markdown」页面，docx → md 本地转换。

## 选型结论（实测三库，样本含 NPOI 合成、Word COM 生成与两份真实接口文档）
- 定选 `DocSharp.Docx 0.21.0`（MIT，net10 dll 共约 0.93MB）：唯一能按 `styles.xml w:name` 解析标题的候选，中文版 Word 本地化数字 styleId（`1`/`2`/`a0`）也能正确输出 `#` 层级；图片支持目录导出。
- 落选：`DocumentFormat.OpenXml.Markdown 1.0.1.1`（DocumentParser.cs 只匹配 pStyle 字面 `Heading` 前缀，真实 Word 文档标题全丢）；`OfficeIMO.Word.Markdown 3.6.2`（同样丢标题，且默认 base64 内联图片致输出膨胀 25 倍）；pandoc（41MB 外部进程，与单 exe 分发冲突）。

## 落地要点
- `Services/Word/WordToMarkdownService`：DocSharp `ImagesOutputFolder` + `ImagesBaseUriOverride` 双设置产出相对链接（缺省会写 `file:///` 绝对链接）；`WordMarkdownPostProcessor` 清洗 `_Toc` 书签空锚点。
- 命名陷阱：新增 `AzrngTools.Models.Word/Services.Word` 命名空间遮蔽了 PdfPig 的 `Content.Word`，`PdfPageLayoutAnalyzer` 加 `using PdfWord = ...` 别名修复。
- 测试陷阱：NPOI 裸 `XWPFDocument` 不写 `styles.xml`，fixture 需 `CreateStyles()` + `CT_Style(name="heading N")` 显式补样式定义，否则 DocSharp 无样式名可查、标题断言必红。
- 统计陷阱：DocSharp 只在文档确实含图时才创建 `.assets` 目录，导出计数前须 `Directory.Exists` 判断。

## 验证
- 新增 9 用例全绿（标题/行内格式/表格/图片双模式/锚点清洗/3 失败路径/文件摘要）。
- 端到端：真实接口文档 984KB → 12 标题 / 11 图片全部落 `.assets`、相对引用、无锚点残留，转换约 300ms。
- 全量 147 用例：140 通过；7 失败为基线既有（HEAD aa9cdd4 的 PdfSharp 6.5.0 AGPL `PdfSharpLicenseException`，已用干净 worktree 基线对照确认与本次改动无关）。
