---
doc_id: DES-FORMAT-001
doc_type: design
module: format
feature: tools
status: in-review
last_updated: 2026-10-30
related:
  - doc/requirements/format/tools.md
---

# 格式化工具组设计（DES-FORMAT-001）

> 本文档为依据当前实现整理的回溯基线，待用户确认后转为 approved。

## 需求追溯

- 关联需求：`REQ-FORMAT-001`。
- 覆盖导航分组"格式化类工具"下九个工具页；重点展开 JSON/YAML/XML/ENV 互转的服务化设计，其余页面按共性模式说明。

## 技术设计

### 页面清单与职责

| 页面 | ViewModel | 实现要点 |
| --- | --- | --- |
| 文本压缩 | `StringPageViewModel` | 去除多余空白，纯 ViewModel 计算 |
| JSON 格式化 | `JsonPageViewModel` | 缩进美化与压缩 |
| SQL 操作 | `SqlFormatPageViewModel` | 格式化内核为 `Core/TSqlFormatter` |
| XML 转 HTML | `XmlToHtmlPageViewModel` | 基于 XSLT；AOT 版本不可用，页面明确提示 |
| JSON/YAML/XML/ENV 互转 | `JsonYamlXmlPageViewModel` | 见下文转换服务设计 |
| 正则表达式测试 | `RegexAnalysisViewModel` | 正则编辑与匹配测试 |
| 字数统计 | `WordCountPageViewModel` | 文本统计，纯 ViewModel 计算 |
| 人民币大写转换 | `RMBConvertPageViewModel` | 数字金额转大写 |
| MIME 类型查询 | `MimeQueryPageViewModel` | 扩展名 / MIME 映射查询 |

简单转换页（压缩、统计、大写、MIME）不设服务层，逻辑在 ViewModel；格式化与转换内核下沉的部分（SQL、三格式互转）收敛为独立内核以便测试。

### JSON/YAML/XML/ENV 互转服务设计

`Services/Format/FormatConversionService`（`IFormatConversionService`，Singleton）：

```text
源文本 ──解析──▶ JsonNode 中间模型 ──序列化──▶ 目标格式
```

- **统一中间模型**：四种格式先解析为 `System.Text.Json.Nodes.JsonNode`，再由中间模型输出目标格式，避免两两直转的组合爆炸（4 种格式只需 4 个解析器 + 4 个输出器）。
- **XML 约定**：属性映射为 `@名称` 键，元素文本映射为 `#text` 键，与常见转换工具约定一致。
- **能力接口**：`Convert(source, from, to)` 互转、`Format(source, language)` 格式化、`Validate(source, language)` 校验（返回 `ConversionCheckResult`）。
- **错误约定**：解析失败抛 `FormatException` / `ArgumentException`，消息携带行号信息；服务不吞异常，由 ViewModel 统一捕获后更新校验信息并经 `MessageService` 提示。
- **ViewModel 编排**：空输入提示；源与目标格式相同时提示改用"格式化"；结果区区分成功与失败信息。

### ENV 转换设计（2026-10-30 新增）

- **格式落点**：`FormatLanguage` 枚举新增 `Env`，页面两个格式下拉直接绑定枚举（`EnumComboBox`），无需改视图结构；菜单与页面文案同步改名"JSON/YAML/XML/ENV 互转"。
- **解析器（ENV→JsonNode）**：逐行解析为扁平 `JsonObject`（值全为字符串）。覆盖 dotenv 通行语法：整行注释与行尾注释（行尾 `#` 需有前置空白才视为注释，`http://a#b` 类值不受影响）、可选 `export ` 前缀、键值两侧空白裁剪、单引号（字面量）与双引号（处理 `\n` `\t` `\"` `\\` 转义）、跨行引号值（未闭合引号继续吞行直到闭合）、空值；重复键后值覆盖。`${VAR}` 不做插值。解析失败抛 `FormatException` 并携带行号。
- **输出器（JsonNode→ENV）**：对象按点路径扁平化（`A.B`），数组按下标路径（`A.0`）；字符串值仅在必要时加双引号（为空、含空白 / `#` / 引号 / 换行 / 首尾空格），其余裸值；数字、布尔按字面量，null 输出空值。
- **不对称约定（写入 REQ）**：ENV→JSON 保持扁平与键原文，JSON→ENV 才做扁平化；含点的键在 ENV 侧永远是普通键，往返以 JSON 侧为结构真相源。
- **格式化与校验**：`Format(env)` 等价于重新规范化输出；`Validate(env)` 走解析器，失败原因含行号。

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

- 单元测试：`FormatConversionService` 的互转、格式化、校验与异常路径（`AzrngTools.Tests/Services/Format/`），ENV 侧覆盖：注释 / 引号 / 跨行值 / 重复键 / 行尾注释边界、扁平化输出与按需引号、JSON↔ENV 往返、非法输入行号提示。
- 手动 smoke：JSON / SQL 格式化往返、四格式互转样例、非法输入报错、AOT 发布版本验证 XML 转 HTML 的提示行为。
