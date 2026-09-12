---
doc_id: DES-MISC-001
doc_type: design
module: misc
feature: tools
status: in-review
last_updated: 2026-09-12
related:
  - doc/requirements/misc/tools.md
---

# 其他工具设计（DES-MISC-001）

> 本文档为依据当前实现整理的回溯基线，待用户确认后转为 approved。

## 需求追溯

- 关联需求：`REQ-MISC-001`。
- 覆盖导航分组"其他"下两个工具页：Unix 时间戳与翻译。

## 技术设计

### Unix 时间戳

- `Views/Other/UnixTimestampPageView` + `UnixTimestampPageViewModel`，单页双向换算，无服务层、无持久化、无网络。
- 输入时间戳换算日期时间，或输入日期时间换算时间戳；结果可复制。
- 非法输入提示原因，不输出结果；时间戳精度以页面实现为准（见需求待确认项）。

### 翻译

| 模块 | 生命周期 | 职责 |
| --- | --- | --- |
| `TranslatorPageViewModel` | 页面级 | 输入、翻译命令、结果展示与失败提示 |
| `TranslationService` | Transient | 翻译内核：基于 `GTranslate` 库的 Yandex 翻译器，当前提供中英互译方法 |

```text
输入文本 → TranslatorPageViewModel → TranslationService（Yandex，在线）
                                      ├── 成功 → 展示译文
                                      └── 失败（网络 / 服务不可达）→ 可读错误，输入保留可重试
```

- 翻译是工具箱中少数依赖外部网络服务的能力：输入内容会提交给翻译服务，仅用于翻译本身；失败时明确提示，不做静默重试。

## 交互与异常

| 场景 | 处理 |
| --- | --- |
| 输入为空 | 提示补全 |
| 非法时间戳 / 日期 | 提示原因 |
| 翻译服务不可达 | 展示失败原因，输入保留，可重试 |
| 换算 / 翻译成功 | 输出可一键复制 |

## 验证方式

- 手动 smoke：时间戳与日期时间双向换算正确性；联网翻译中 → 英与英 → 中；断网翻译的失败提示与重试。
