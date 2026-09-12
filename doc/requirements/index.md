# 需求索引

## 项目目标

AzrngTools 是面向开发与效率场景的 Windows 桌面工具箱（.NET 10 + Avalonia 12），把高频开发工具收拢为本地应用：数据不出本机、离线可用、即开即用。当前提供 33 个工具入口，覆盖数据库、接口调试、PDF 处理、生成与加密、编码解码、格式化、时间与翻译、设置与更新等场景，另有一个已暂缓的剪贴板历史规划模块。

技术栈与构建发布方式见根目录 `README.md`；跨模块架构见 `doc/design/architecture.md`。

## 通用约束

- 单机本地使用：除翻译与更新检查外，工具能力均本地完成，不引入账号与权限体系。
- 隐私优先：加解密、编码转换等涉敏输入不出本机，工具页不持久化用户输入。
- AOT 兼容：发布采用 Native AOT 单文件；依赖运行时代码生成的能力需明确提示不可用。
- 界面遵循 `design-system.yaml` 与 `Styles/` 设计 token 约定。

## 模块目录

| 模块 | 功能 | 需求文档 | 状态 |
| --- | --- | --- | --- |
| database | 数据库工作台（连接、对象浏览、SQL、导出、代码生成） | [database/workbench.md](database/workbench.md) | in-review |
| network | 接口调试（请求配置、发送取消、历史） | [network/api-debug.md](network/api-debug.md) | in-review |
| pdf | PDF 管理（页码分割、导出 Word） | [pdf/management.md](pdf/management.md) | in-review |
| generation | 生成与加密工具组（GUID、HASH、HMAC、AES、DES、SM4、RSA、Json Schema、JSON 转 C#、密码生成） | [generation/tools.md](generation/tools.md) | in-review |
| encode | 编码 / 解码工具组（Base64、URL、Unicode、Hex、繁简、JWT、Gzip、组合工作台） | [encode/tools.md](encode/tools.md) | in-review |
| format | 格式化工具组（文本压缩、JSON、SQL、XML 转 HTML、JSON/YAML/XML 互转、正则、字数、人民币大写、MIME） | [format/tools.md](format/tools.md) | in-review |
| misc | 其他工具（Unix 时间戳、翻译） | [misc/tools.md](misc/tools.md) | in-review |
| settings | 设置与应用管理（硬件信息、关于、自动更新、主题、常用入口） | [settings/app.md](settings/app.md) | in-review |
| clipboard | 剪贴板历史管理（已暂缓，文档保留待恢复） | [clipboard/history.md](clipboard/history.md) | draft（暂缓） |

> 首页概览与侧栏导航属于应用框架，不单独立需求文档，见 `doc/design/architecture.md`。

## 状态说明

- 除 clipboard 外的模块文档均为 2026-09-12 依据当前实现整理的回溯基线，描述现状而非新方案；用户确认后转 `approved`。
- `clipboard` 为阶段 0 已完成、用户决定暂缓的规划功能，实现不在当前版本范围内。

## 兼容入口

- `doc/requirement.md`：数据库工作台界面重设计的历史需求单文件，已迁移至 `database/workbench.md`，仅兼容保留，不再追加新章节。
