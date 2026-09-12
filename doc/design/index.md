# 设计索引

> 设计文档按业务模块组织；跨模块公共架构统一记录在 `architecture.md`。

| 模块 | 功能 | 设计文档 | 状态 | 关联需求 |
| --- | --- | --- | --- | --- |
| app | 跨模块架构（应用壳、导航、持久化、更新与发布链路） | [architecture.md](architecture.md) | in-review | — |
| database | 数据库工作台界面重设计 | [database/workbench.md](database/workbench.md) | in-review | `REQ-DATABASE-001` |
| network | 接口调试 | [network/api-debug.md](network/api-debug.md) | in-review | `REQ-NETWORK-001` |
| pdf | PDF 管理 | [pdf/management.md](pdf/management.md) | in-review | `REQ-PDF-001` |
| generation | 生成与加密工具组 | [generation/tools.md](generation/tools.md) | in-review | `REQ-GENERATION-001` |
| encode | 编码 / 解码工具组 | [encode/tools.md](encode/tools.md) | in-review | `REQ-ENCODE-001` |
| format | 格式化工具组 | [format/tools.md](format/tools.md) | in-review | `REQ-FORMAT-001` |
| misc | 其他工具 | [misc/tools.md](misc/tools.md) | in-review | `REQ-MISC-001` |
| settings | 设置与应用管理 | [settings/app.md](settings/app.md) | in-review | `REQ-SETTINGS-001` |
| clipboard | 剪贴板历史管理（已暂缓，文档保留） | [clipboard/history.md](clipboard/history.md) | draft（暂缓） | `REQ-CLIPBOARD-001` |

## 状态说明

- 除 clipboard 外的设计文档均为 2026-09-12 依据当前实现整理的回溯基线，用户确认后转 `approved`。
- 各模块的 DTO / 数据结构以代码为最终事实来源，设计文档只引用代码路径，不维护第二份字段表；新增契约遵循 `doc-AGENTS.md` 的契约生命周期。

## 兼容入口

- `doc/design/设计文档.md`：数据库工作台界面重设计的历史设计单文件，已迁移至 `database/workbench.md`，仅兼容保留，不再追加新章节。
