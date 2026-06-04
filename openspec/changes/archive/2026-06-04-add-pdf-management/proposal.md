## Why

AzrngTools 当前缺少面向 PDF 的文件处理入口，用户需要离开工具箱完成 PDF 分割和 Word 导出，打断日常文档处理流程。
新增 PDF 管理界面可以补齐常见办公文件处理能力，并与现有工具页保持统一的本地、轻量、可验证体验。

## What Changes

- 新增 PDF 管理页面入口，支持选择本地 PDF 文件并展示文件基础信息、页数和处理状态。
- 支持按页码范围或指定页码将 PDF 分割导出为一个或多个 PDF 文件。
- 支持将当前 PDF 导出为 Word 文档，并提供导出路径选择、进度状态和结果反馈。
- 新增 PDF 处理服务抽象，基于 `Aspose.PDF.Drawing` NuGet 包封装文件校验、分割、导出 Word 和错误转换，避免 ViewModel 直接依赖第三方库细节。
- 新增 PDF 管理 ViewModel、页面视图、导航注册和最小业务测试。

## Capabilities

### New Capabilities
- `pdf-management`: 定义 PDF 文件导入、分割导出、Word 导出、状态反馈和错误处理能力。

### Modified Capabilities

无。

## Impact

- 影响主导航菜单、工具统计和 PDF 管理页面相关 View / ViewModel。
- 需要新增应用层服务接口与实现，用于 PDF 分割和 Word 导出。
- 使用 `Aspose.PDF.Drawing` NuGet 包作为 PDF 分割与 Word 导出的核心依赖（原本地 DLL v18.11 在 .NET 10 上运行时崩溃，已替换）。需验证授权配置、评估版限制和当前 .NET/Avalonia 桌面应用发布兼容性。
- 需要补充单元测试或等价验证，覆盖页码解析、文件校验、分割参数转换、导出失败反馈和成功路径。
