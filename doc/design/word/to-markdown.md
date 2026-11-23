---
doc_id: DES-WORD-001
doc_type: design
module: word
feature: to-markdown
status: in-review
last_updated: 2026-11-23
related:
  - doc/requirements/word/to-markdown.md
---

# Word 转 Markdown 设计（DES-WORD-001）

> 依据 2026-11-23 调研结论（三库实测：DocSharp.Docx 为唯一能正确解析中文版 Word 本地化样式的候选）与用户确认的实现方向整理。

## 需求追溯

- 关联需求：`REQ-WORD-001`。
- 本次覆盖：docx 载入、Markdown 转换（含图片导出 / 忽略两种模式）、锚点清洗、页面预览与导出。
- 不覆盖：.doc 输入、反向转换、公式与 SmartArt 完整还原。

## 契约引用

- 请求 / 结果模型以 `Models/Word/` 下类型为最终事实来源（本模块无跨模块契约，不立 CON- 编号，以代码为准）。

## 技术设计

### 模块职责

| 模块 | 职责 |
| --- | --- |
| `WordMarkdownPageViewModel` | 页面编排：文件载入、输出路径与图片模式选择、转换命令、结果反馈与预览文本管理 |
| `WordToMarkdownService` | 转换内核封装：参数校验、DocSharp 调用、图片目录与相对链接配置、输出落盘、结果统计与错误包装 |
| `WordMarkdownPostProcessor` | 纯静态后处理：清洗 Word 目录书签锚点等已知噪声 |

### 数据流向

```text
选择本地 docx → 文件摘要（路径/大小）
        ↓ 确认输出路径 + 图片模式
WordToMarkdownService.ConvertAsync(request)
  ├── 校验失败 → IResultModel 失败（中文原因），流程终止
  └── DocSharp DocxToMarkdownConverter
        ├── ImagesOutputFolder = <md名>.assets（导出模式）
        ├── ImagesBaseUriOverride = "<md名>.assets/"（相对链接）
        └── MarkdownStringWriter 输出
              ↓
        WordMarkdownPostProcessor（清洗 _Toc 锚点）
              ↓
        写出 .md + 统计（标题数/图片数）
              ↓
        结果反馈（成功含预览文本 / 失败原因）
```

### 关键技术决策

1. **转换内核 DocSharp.Docx 0.21.0**（2026-11-23 实测三库对比定选）：
   - 中文版 Word 写入的 docx 标题样式 styleId 为本地化数字 ID（如 `1`、`2`），语义在 `styles.xml` 的 `w:name="heading N"`；DocSharp 按 `w:name` 解析（实测真实接口文档 12 个标题全部正确输出 `#`/`##`/`###`）；对照候选 DocumentFormat.OpenXml.Markdown 1.0.1.1 仅匹配 styleId 字面 `Heading` 前缀（真实文档标题全部丢失）、OfficeIMO.Word.Markdown 3.6.2 同样丢失标题且默认 base64 内联图片使输出膨胀。
   - 体积增量约 0.93MB（DocSharp.Docx 723KB + DocSharp.Common 208KB），传递依赖 DocumentFormat.OpenXml 与 NPOI 无冲突，不重新独立引用。
   - 已知噪声与对策：`<a id="_Toc…"></a>` 书签锚点（后处理正则清洗）；`string\(64\)` 反斜杠转义（GFM 渲染正确，保留）；`<u>` 下划线 HTML（GFM 内联 HTML 合法，保留）；Word 目录域文字作为普通段落（三家行为一致，保留）。
2. **图片导出到 md 同级 `<md 名>.assets/` + 相对链接**（2026-11-23 实测确认）：DocSharp 默认写 `file:///` 绝对链接（文档拷走即断链），必须同时设 `ImagesOutputFolder`（绝对路径，自动创建）与 `ImagesBaseUriOverride`（相对前缀，正斜杠）。忽略模式不设这两个属性，DocSharp 行为为整体丢弃图片（实测图片行=0）。
3. **结果包装**：对齐 PDF 域约定，Service 不抛异常，`WordResultModel`（错误码 `WORD_ERROR`）+ `BuildFriendlyError` 分层；ViewModel `HandleResult` / `RunBusyAsync` 复用既有模式。
4. **预览用纯文本**：TextBox 显示 Markdown 源文本；不启用 Markdown.Avalonia 渲染（该组件 Avalonia 12 兼容未验证，菜单入口已隐藏，不为本功能引入新风险）。

### 状态变化

- `CurrentFile`：null → 载入摘要（文件名 / 大小 / 路径）；
- `PreviewText`：转换成功后填充 Markdown 源文本（预览区可见），失败清空；
- `IsBusy` / `BusyText` / `StatusText` / `LastResultText`：与 PDF 页一致的忙态与反馈链路；
- 输入记忆（`REQ-SETTINGS-002`）：`ExportImages`（图片模式）与 `OutputPath` 入存档，`CurrentFile` 不入存档（对齐 PDF 页口径）；pageId `word-to-markdown`。

## 界面原型

沉淀型真栈原型：`Views/Word/WordMarkdownPageView.axaml` 直接在正式工程实现，布局复用 PDF 管理页双列骨架。

```text
┌──────────────────────────────┬──────────────────────────────┐
│ Word 文件                     │ 导出设置                       │
│ [选择 Word]  摘要卡(名/大小)   │ 输出路径 [_______] [选择]      │
│                              │ [x] 导出图片到 .assets 目录    │
├──────────────────────────────┤ (关闭=忽略图片)                │
│ 处理状态                      │ [开始转换]  [清空当前]          │
│ 状态文本 / 进度条(忙态) / 结果  ├──────────────────────────────┤
│                              │ Markdown 预览                  │
│                              │ ┌──────────────────────────┐ │
│                              │ │ # 标题                    │ │
│                              │ │ 正文…（源文本，只读滚动）   │ │
│                              │ └──────────────────────────┘ │
└──────────────────────────────┴──────────────────────────────┘
loading：进度条 + BusyText；empty：预览区占位提示"转换后此处显示结果"；
error：状态区显示失败原因（红色语义由样式类承载）
```

## 交互与异常

| 场景 | 处理 |
| --- | --- |
| 源文件不存在 / 非 .docx | 校验失败，展示具体原因，不进入转换 |
| 输出路径为空 | 提示"请选择 Markdown 输出文件"，不进入转换 |
| docx 损坏 / 非法 OPC 包 | DocSharp 抛异常 → `BuildFriendlyError` 转可读原因 + 日志 |
| 转换中 | 进行中反馈，不阻塞界面 |
| 图片导出失败（磁盘满 / 权限） | 转换失败并展示原因（与 md 写出同事务边界） |
| 转换成功 | 提示输出路径 + 标题数 + 图片数，预览区填充源文本 |

## 验证方式

- 单元测试（`AzrngTools.Tests/Services/Word/`）：NPOI XWPF 内存生成含标题 / 加粗斜体 / 表格 / 引用 / 图片的 docx fixture → `ConvertAsync` → 断言 md 结构（标题行、`**`、GFM 表格行、图片相对引用、assets 目录文件数）；忽略模式断言无图片引用且无 assets 目录；失败路径（不存在文件 / 非 docx）断言失败消息。
- 构建：解决方案 0 错 0 警告；全量测试通过。
- 端到端：本机真实 Word 文档（含中文标题样式、内嵌图片）转换后核对标题层级与图片相对链接。
