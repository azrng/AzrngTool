---
doc_id: CON-SETTINGS-001
doc_type: contract
module: settings
feature: page-input-archive
status: superseded
last_updated: 2026-11-05
related:
  - doc/requirements/settings/page-input-archive.md
  - doc/design/settings/page-input-archive.md
---

# 页面输入存档文件契约草案（CON-SETTINGS-001）

- 来源路径（最终事实来源）：`AzrngTools/Services/PageInputStoreService.cs`
- 状态：superseded（T038 已落地，结构以代码为准；下方草案保留作历史记录）

> 变更记录：2026-11-05 实现落地。与草案一致：version/updatedAt/inputs 三段结构、camelCase 序列化（源生成）、标量字符串编码、敏感键不落盘、单值 2,000,000 字符截断、损坏整页兜底。

## 存档文件

- 位置：`%LOCALAPPDATA%\AzrngTools\page-inputs\<pageId>.json`，一页一文件，UTF-8；
- pageId 命名：`<分组>-<页面>` kebab-case（清单见 DES-SETTINGS-002），即文件名。

## 文件结构

```json
{
  "version": 1,
  "updatedAt": "2026-11-05T10:00:00.000Z",
  "inputs": {
    "Original": "文本内容原文",
    "IgnoreCase": "true",
    "PasswordLength": "16"
  }
}
```

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| version | int | 结构版本，当前固定 1；未来不兼容变更时递增，读取方遇到未知版本按损坏兜底处理 |
| updatedAt | string (ISO 8601) | 最后保存时间，仅诊断用 |
| inputs | object (string → string?) | 页面声明的记忆键集合；值为字符串编码的标量：bool → `"true"/"false"`，数值 → 不变性文化十进制字符串，枚举选项 → 序号十进制字符串，文本 → 原文；键名与 ViewModel 属性名一致 |

## 约束

- 敏感字段（密钥、令牌、凭证）不写入 inputs（键不存在，而非空值）；
- 单值超 2,000,000 字符跳过该键；
- 读取任何解析失败按整页损坏兜底（返回空字典）。
