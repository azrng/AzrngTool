## Context

AzrngTools 是本地桌面工具箱，现有功能以 Avalonia 页面、ViewModel、Service 和单元测试分层组织。主导航在 `AzrngTools/ViewModels/MainWindowViewModel.cs` 中注册工具入口，具体工具页按业务域分布在 `Views`、`ViewModels` 和 `Services` 下。

PDF 管理属于新增办公文档处理能力，既涉及界面交互，也涉及文件系统、商业 PDF 处理库和发布打包兼容性。当前工程目标框架为 `net10.0-windows`，Release 使用自包含单文件、ReadyToRun、非 Trim/AOT 发布。

### PDF 转 Word 库选型评估（2026-06-04）

对以下 10 个 .NET PDF 库进行了评估：

| 库 | 许可证 | PDF 转 Word | .NET 8/10 | 结论 |
|---|---|---|---|---|
| Aspose.PDF | 商业 (~$1199/开发者) | 支持（最佳） | 是 | 本地 DLL (v18.11) 在 .NET 10 上运行时崩溃，NuGet 21.10.1 同样失败 |
| Spire.PDF | 商业（免费版限 5 页） | 支持 | 是 | 免费版有页数限制，不可用于生产 |
| IronPdf | 商业（有试用） | 支持 | 是 | 需购买许可 |
| iText 7 | AGPL v3 / 商业双许可 | 需商业版 | 是 | AGPL 传染性，商用需购买 |
| iTextSharp | MPL（已废弃） | 不支持 | 否 | 已停维 |
| PDFsharp | MIT | 不支持 | 是 | 仅 PDF 创建/编辑 |
| QuestPDF | MIT | 不支持 | 是 | 仅 PDF 生成 |
| Magick.NET | MIT | 不支持 | 是 | 仅 PDF 渲染为图片 |
| WkHtmlToPdfDotNet | LGPL | 不支持 | 是 | 仅 HTML 转 PDF，底层已停维 |
| DinkToPdf | 开源 | 不支持 | 是 | 仅 HTML 转 PDF，已停维 |

**核心结论**：.NET 生态中不存在同时满足"开源 + 免费商用 + PDF 转 Word + NuGet 可用"的单一库。

**补充方案**：
- `PdfPig` (Apache-2.0) + `OpenXML SDK` (MIT)：纯开源组合，需自行实现文本提取→DOCX 重建流水线，格式还原能力有限
- `LibreOffice Headless`：需安装外部软件，部署复杂

**当前决策**：继续使用 Aspose.PDF.Drawing NuGet 包（评估模式），功能可用但有水印。待用户确认最终方案后替换。

## Goals / Non-Goals

**Goals:**

- 在主导航新增 PDF 管理入口，提供本地 PDF 选择、文件摘要、页码输入、分割导出和 Word 导出能力。
- 将 PDF 处理逻辑放在应用服务层，ViewModel 只负责状态编排、命令可用性、参数收集和用户反馈。
- 基于项目内本地引用的 `Aspose.Pdf.dll` 实现 PDF 页数读取、页面拆分和 `.docx` 导出。
- 支持可测试的页码范围解析和导出参数构建，确保常见错误能以中文提示反馈给用户。
- 在首版实现中优先保证本地离线处理、构建可通过、发布产物可打包，并清晰说明授权配置方式。

**Non-Goals:**

- 不实现云端转换、账号体系、批量队列、历史记录或后台任务管理。
- 不引入 OCR 识别能力，不承诺扫描件转换为可编辑文字。
- 不承诺复杂版式、批注、表单、签名 1:1 完整保留，实际效果以 Aspose 转换能力为准。
- 不修改现有数据库文档导出能力，也不复用数据库导出弹窗作为 PDF 管理入口。
- 不新增持久化配置，除非实现过程中发现现有文件选择体验必须记住目录。

## Decisions

### 1. 新增独立 PDF 管理模块

新增 `PdfManagementPage`、`PdfManagementPageViewModel`、`IPdfProcessingService` 和相关模型，放在独立 PDF/Document 语义目录下，而不是塞入现有数据库导出模块。

原因：数据库导出是结构化元数据导出，PDF 管理是本地文件处理，两者输入输出、错误边界和页面工作流不同。独立模块能避免复用错误职责。

备选方案：复用数据库导出服务或弹窗。放弃原因是会把文件处理和数据库对象选择混在一起，后续维护成本更高。

### 2. ViewModel 只编排状态，服务层封装文件处理

`PdfManagementPageViewModel` 负责选择文件、展示摘要、解析用户输入、调用服务和展示结果；`IPdfProcessingService` 负责 PDF 加载、页数读取、页码校验、分割导出和 Word 导出。

原因：当前项目已有 Service + ViewModel 的分层实践，服务抽象便于用测试替身覆盖 UI 命令逻辑，也能隔离 Aspose API 变化。

备选方案：在 ViewModel 中直接调用 Aspose。放弃原因是会增加界面层职责，测试困难，也不利于后续替换库或集中处理授权。

### 3. 页码输入采用可解释的文本规则

分割规则首版支持两种模式：

- 按范围分割：用户输入 `1-3,5,8-10` 这类页码表达式，导出为一个 PDF。
- 按单页批量分割：用户选择“逐页导出”时，将选中页码拆成多个 PDF。

页码解析结果用独立模型表示，页码越界、格式错误、空输入必须在执行前拦截。

原因：文本规则适合桌面工具高频操作，控件复杂度低，也方便单元测试。

备选方案：做完整页面缩略图拖拽选择。放弃原因是首版实现成本高，且会引入 PDF 渲染缩略图交互复杂度，超出“分割与导出 Word”的核心诉求。

### 4. PDF 处理采用 PdfPig + PDFsharp + OpenXML SDK 开源组合

PDF 元数据读取和页面拆分通过 `PdfSharp` (MIT) 完成，Word 导出通过 `PdfPig` (Apache-2.0) + `DocumentFormat.OpenXml` (MIT) 完成。服务层负责封装文件校验、页面复制、文本提取→DOCX 重建和异常转换，界面层不直接引用第三方库 API。

原因：原方案使用的本地 `Aspose.Pdf.dll` (v18.11) 在 .NET 10 运行时崩溃，NuGet 官方 `Aspose.PDF` 21.10.1 同样失败，`Aspose.PDF.Drawing` 22.12.0 需要商业授权且评估模式有水印。改用开源组合后无需授权，功能完整可用。

备选方案：
- Aspose.PDF.Drawing NuGet 包：需要商业授权，评估模式有水印
- Spire.PDF 免费版：限 5 页，不可用于生产
- iText 7：AGPL 传染性，商用需购买

### 5. 改用 NuGet 包引用，移除本地 DLL

将 `AzrngTools.csproj` 中的本地 DLL 引用 `<Reference Include="Aspose.Pdf">` 替换为 NuGet 包引用 `<PackageReference Include="PdfPig" />`、`<PackageReference Include="PdfSharp" />` 和 `<PackageReference Include="DocumentFormat.OpenXml" />`，并删除 `AzrngTools/Libs/Aspose/Aspose.Pdf.dll`。

原因：本地 DLL 在 .NET 10 上无法运行，Aspose 商业库需要授权且评估模式有水印。改用开源 NuGet 包无需授权，兼容性好，且享受 NuGet 的依赖管理和版本更新。

### 6. 无需授权管理

改用 PdfPig (Apache-2.0) + PDFsharp (MIT) + OpenXML SDK (MIT) 开源组合后，所有依赖均为免费开源许可，无需商业授权管理。

原因：开源库无授权限制，功能完整可用，不存在评估模式水印或页数限制问题。

## Risks / Trade-offs

- [Resolved] ~~未配置 Aspose 授权时导出结果带评估水印或页数受限~~ → Resolution: 改用开源组合，无授权限制。
- [Risk] Word 导出版式与原 PDF 不完全一致 → Mitigation: 使用 PdfPig 提取文本按行重组为 DOCX，并在界面提示复杂版式转换可能存在差异。
- [Risk] 扫描件 PDF 导出的 Word 不具备可编辑文本 → Mitigation: 首版不引入 OCR，必要时提示扫描件/OCR 不在当前范围。
- [Risk] Aspose.PDF.Drawing 在特定 PDF 格式下转换失败 → Mitigation: 异常已通过 `LocalLogHelper.LogError` + `ex.GetExceptionAndStack()` 记录完整堆栈，`BuildFriendlyError` 对无意义异常消息提供友好提示。
- [Risk] 大文件处理阻塞界面 → Mitigation: 导出命令使用异步执行，ViewModel 暴露忙碌状态并禁用重复操作。
- [Risk] 页码表达式歧义导致误导出 → Mitigation: 执行前解析并回显选中页数，错误时不调用服务。
- [Resolved] 本地 Aspose.Pdf.dll (v18.11) 在 .NET 10 上运行时崩溃 → Resolution: 改用 Aspose.PDF.Drawing NuGet 包。

## Migration Plan

1. 新增 PDF 管理 spec、页面、ViewModel、服务接口、服务实现和测试。
2. 在主导航中注册 PDF 管理入口，并更新首页工具描述或统计来源中的相关文案。
3. ~~将 `C:\Downloads\Aspose.Pdf.dll` 复制到项目内约定目录~~ → 改用 `PdfPig` + `PdfSharp` + `DocumentFormat.OpenXml` NuGet 包，移除本地 DLL 引用。
4. ~~新增授权加载说明~~ → 改用开源组合，无需授权管理。
5. 若 Aspose 评估模式无法满足需求，回退策略是移除导航入口和依赖，保留 OpenSpec 记录并重新评估其他方案（PdfPig+OpenXML、Spire.PDF、商业许可）。

## Open Questions

- 分割结果是否需要支持”每个范围单独导出一个文件”，还是首版只支持”选中页合并为一个文件”和”逐页导出”？
- 是否需要记住上次选择的输入/输出目录？
