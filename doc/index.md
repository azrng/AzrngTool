# 文档索引

> 本文件只做导航，不承载具体内容。

## 文档目录

```text
doc/
├── index.md                        # 本文件
├── requirements/                   # 需求文档（REQ-）
│   └── index.md                    # 项目目标与模块目录
├── design/                         # 设计文档（DES-）
│   ├── index.md                    # 设计导航
│   └── architecture.md             # 跨模块架构设计
└── contracts/                      # 契约草案（CON-）
```

## 模块与功能

| 模块 | 功能 | 需求 | 设计 | 契约 | 状态 |
| --- | --- | --- | --- | --- | --- |
| app（应用框架） | 主窗口导航、更新、发布等跨模块架构 | — | [architecture](design/architecture.md) | 以代码为准 | in-review |
| database | 数据库工作台 | [workbench](requirements/database/workbench.md) | [workbench](design/database/workbench.md) | 以代码为准 | in-review |
| network | 接口调试 | [api-debug](requirements/network/api-debug.md) | [api-debug](design/network/api-debug.md) | 以代码为准 | in-review |
| pdf | PDF 管理（分割 / 转 Word） | [management](requirements/pdf/management.md) | [management](design/pdf/management.md) | 以代码为准 | in-review |
| generation | 生成与加密工具组（10 页） | [tools](requirements/generation/tools.md) | [tools](design/generation/tools.md) | 以代码为准 | in-review |
| encode | 编码 / 解码工具组（9 页） | [tools](requirements/encode/tools.md) | [tools](design/encode/tools.md) | 以代码为准 | in-review |
| format | 格式化工具组（9 页） | [tools](requirements/format/tools.md) | [tools](design/format/tools.md) | 以代码为准 | in-review |
| misc | 其他工具（时间戳 / 翻译） | [tools](requirements/misc/tools.md) | [tools](design/misc/tools.md) | 以代码为准 | in-review |
| settings | 设置与应用管理（硬件 / 关于 / 更新 / 主题） | [app](requirements/settings/app.md) | [app](design/settings/app.md) | 以代码为准 | in-review |
| clipboard | 剪贴板历史管理（已暂缓，文档保留） | [history](requirements/clipboard/history.md) | [history](design/clipboard/history.md) | `CON-CLIPBOARD-001`（draft） | draft（暂缓） |

## 状态说明

- 除 clipboard 外，各模块文档均为 2026-09-12 依据当前实现整理的回溯基线：内容描述现状而非新方案，待用户确认后转 `approved`。
- `clipboard` 为用户决定暂缓的规划功能（阶段 0 文档，未实现），文档保留待后续恢复，见 `REQ-CLIPBOARD-001`。
- 契约列"以代码为准"表示该模块的 DTO / 数据结构以代码为最终事实来源，未单独立契约文档。

## 兼容入口

以下旧单文件已迁移至 `database/workbench` 文档对，仅作兼容保留，新内容不再追加：

- `doc/requirement.md` → `doc/requirements/database/workbench.md`
- `doc/design/设计文档.md` → `doc/design/database/workbench.md`
