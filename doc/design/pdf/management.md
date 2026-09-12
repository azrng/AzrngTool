---
doc_id: DES-PDF-001
doc_type: design
module: pdf
feature: management
status: in-review
last_updated: 2026-09-12
related:
  - doc/requirements/pdf/management.md
---

# PDF 管理设计（DES-PDF-001）

> 本文档为依据当前实现整理的回溯基线，待用户确认后转为 approved。

## 需求追溯

- 关联需求：`REQ-PDF-001`。
- 本次覆盖：PDF 载入、页码范围解析、分割导出与 Word 导出。
- 不覆盖：合并、加密、水印、OCR 等范围外能力。

## 契约引用

- 页选择与导出请求 / 结果模型以 `Models/Pdf/` 下类型为最终事实来源。

## 技术设计

### 模块职责

| 模块 | 职责 |
| --- | --- |
| `PdfManagementPageViewModel` | 页面向导编排：文件载入、范围输入、导出命令、结果反馈 |
| `AsposePdfProcessingService` | PDF 处理内核：分割与 Word 转换，基于本地 DLL `AzrngTools/Libs/Aspose/Aspose.Pdf.dll`；授权文件 `Aspose.Pdf.lic` 放应用目录，缺失时走 Aspose 评估模式 |
| `PdfPageRangeParser` | 纯静态解析器：把范围表达式解析为页码集合，失败返回明确原因，不抛异常 |

### 页码范围解析规则

- 表达式由逗号分隔，段为单页（`3`）或区间（`3-5`）；
- 解析结果用有序集合去重、排序后输出；
- 页数无效、表达式为空、格式非法等情形返回失败结果并携带可读原因（如"请输入页码范围""当前 PDF 页数无效"），由页面直接展示。

### 数据流向

```text
选择本地 PDF → 读取页数
        ↓ 输入范围表达式
PdfPageRangeParser.Parse(表达式, 页数)
        ├── 失败 → 展示原因，流程终止
        └── 成功 → 页码集合
                    ↓
        AsposePdfProcessingService
          ├── 分割导出 → 输出新 PDF
          └── Word 导出 → 输出 docx
                    ↓
        结果反馈（成功 / 失败原因）
```

### 关键技术决策

1. **本地 DLL 引用而非 NuGet**：Aspose 授权跟随应用目录的 `Aspose.Pdf.lic`；升级 Aspose 版本时替换 DLL 并完成构建与发布验证（见 README）。
2. **解析与处理分离**：范围解析失败在进入 Aspose 处理前短路，避免无效处理；解析器为纯函数，便于单元测试。
3. **评估模式不做应用层拦截**：无授权时的水印与页数限制属于依赖组件自身行为，应用层仅在文档中说明，不伪造授权判断。

## 交互与异常

| 场景 | 处理 |
| --- | --- |
| 源文件损坏 / 加密 | 处理失败，展示可读原因 |
| 范围非法 | 解析阶段短路，展示具体原因，不产生输出文件 |
| 重复页码 / 乱序区间 | 去重排序后正常执行 |
| 处理中 | 进行中反馈，不阻塞界面 |

## 验证方式

- 单元测试：`PdfPageRangeParser` 的合法 / 非法表达式用例（单页、区间、混合、越界、空值）。
- 手动 smoke：真实 PDF 分割后核对页数与内容；Word 导出可打开编辑；无 `Aspose.Pdf.lic` 时确认评估模式行为与提示。
