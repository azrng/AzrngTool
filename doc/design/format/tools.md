---
doc_id: DES-FORMAT-001
doc_type: design
module: format
feature: tools
status: in-review
last_updated: 2026-09-12
related:
  - doc/requirements/format/tools.md
---

# 格式化工具组设计（DES-FORMAT-001）

> 本文档为依据当前实现整理的回溯基线，待用户确认后转为 approved。

## 需求追溯

- 关联需求：`REQ-FORMAT-001`。
- 覆盖导航分组"格式化类工具"下九个工具页；重点展开 JSON/YAML/XML 互转的服务化设计，其余页面按共性模式说明。

## 技术设计

### 页面清单与职责

| 页面 | ViewModel | 实现要点 |
| --- | --- | --- |
| 文本压缩 | `StringPageViewModel` | 去除多余空白，纯 ViewModel 计算 |
| JSON 格式化 | `JsonPageViewModel` | 缩进美化与压缩 |
| SQL 操作 | `SqlFormatPageViewModel` | 格式化内核为 `Core/TSqlFormatter` |
| XML 转 HTML | `XmlToHtmlPageViewModel` | 基于 XSLT；AOT 版本不可用，页面明确提示 |
| JSON/YAML/XML 互转 | `JsonYamlXmlPageViewModel` | 见下文转换服务设计 |
| 正则表达式测试 | `RegexAnalysisViewModel` | 正则编辑与匹配测试 |
| 字数统计 | `WordCountPageViewModel` | 文本统计，纯 ViewModel 计算 |
| 人民币大写转换 | `RMBConvertPageViewModel` | 数字金额转大写 |
| MIME 类型查询 | `MimeQueryPageViewModel` | 扩展名 / MIME 映射查询 |

简单转换页（压缩、统计、大写、MIME）不设服务层，逻辑在 ViewModel；格式化与转换内核下沉的部分（SQL、三格式互转）收敛为独立内核以便测试。

### JSON/YAML/XML 互转服务设计

`Services/Format/FormatConversionService`（`IFormatConversionService`，Singleton）：

```text
源文本 ──解析──▶ JsonNode 中间模型 ──序列化──▶ 目标格式
```

- **统一中间模型**：三种格式先解析为 `System.Text.Json.Nodes.JsonNode`，再由中间模型输出目标格式，避免两两直转的组合爆炸（3 种格式只需 3 个解析器 + 3 个输出器）。
- **XML 约定**：属性映射为 `@名称` 键，元素文本映射为 `#text` 键，与常见转换工具约定一致。
- **能力接口**：`Convert(source, from, to)` 互转、`Format(source, language)` 格式化、`Validate(source, language)` 校验（返回 `ConversionCheckResult`）。
- **错误约定**：解析失败抛 `FormatException` / `ArgumentException`，消息携带行号信息；服务不吞异常，由 ViewModel 统一捕获后更新校验信息并经 `MessageService` 提示。
- **ViewModel 编排**：空输入提示；源与目标格式相同时提示改用"格式化"；结果区区分成功与失败信息。

单元测试位于 `AzrngTools.Tests/Services/Format/`，覆盖转换、格式化与校验路径。

## 交互与异常

| 场景 | 处理 |
| --- | --- |
| 输入为空 | 提示补全，不执行操作 |
| 解析失败（JSON / YAML / XML / 正则 / SQL） | 展示含行号或位置的可读原因，不输出半成品结果 |
| 源目标格式相同（互转页） | 提示无需转换，引导使用格式化 |
| AOT 版本使用 XML 转 HTML | 页面提示该能力不可用 |
| 操作成功 | 输出区更新与校验信息转为成功态，支持一键复制 |

## 验证方式

- 单元测试：`FormatConversionService` 的互转、格式化、校验与异常路径（`AzrngTools.Tests/Services/Format/`）。
- 手动 smoke：JSON / SQL 格式化往返、三格式互转样例、非法输入报错、AOT 发布版本验证 XML 转 HTML 的提示行为。
