---
doc_id: CON-CLIPBOARD-001
doc_type: contract
module: clipboard
feature: history
status: draft
last_updated: 2026-09-12
related:
  - doc/requirements/clipboard/history.md
  - doc/design/clipboard/history.md
---

# 剪贴板历史契约草案（CON-CLIPBOARD-001）

> 状态：阶段 0 草案。阶段 1 落地为项目内模型类型后，代码成为最终事实来源，本文件标记为 `superseded`，只保留来源链接与变更记录。

## 契约范围

定义剪贴板历史模块的核心数据结构与本地存储文件格式，约束阶段 1 的模型定义与阶段 2 的存储实现。不含任何网络接口。

## 存储目录布局

```text
%LocalAppData%\AzrngTools\clipboard\
├── history.json          # 全部条目元数据与文本内容，单文件
└── images\               # 图片条目缓存目录
    └── <entryId>.png     # 以条目 Id 命名的 PNG 文件
```

## 条目类型枚举

| 取值 | 含义 | 归类优先级 |
| --- | --- | --- |
| `Files` | 一次复制 / 剪切的文件或文件夹路径列表 | 1（最高） |
| `Image` | 位图内容 | 2 |
| `Text` | 纯文本 | 3 |

同一次复制携带多种格式时，按上表优先级只归类为一种类型。

## ClipboardEntry 字段

| 字段 | 类型 | 含义与取值规则 |
| --- | --- | --- |
| `Id` | `string` | 条目唯一标识，使用 GUID（"N" 格式），同时作为图片文件名 |
| `Kind` | `ClipboardEntryKind` | 条目类型，见上表 |
| `Text` | `string?` | 仅 `Text` 类型有值；原始文本，超过 32KB 截断存储并置 `Truncated = true` |
| `Preview` | `string` | 列表预览文本：文本取前 200 字符（换行替换为空格）；图片取空串；文件取首路径 |
| `ImageRelativePath` | `string?` | 仅 `Image` 类型有值；相对 `clipboard\` 目录的图片路径，如 `images\<id>.png` |
| `ImagePixelWidth` | `int` | 仅 `Image` 类型有值；图片原始像素宽，用于列表展示 |
| `ImagePixelHeight` | `int` | 仅 `Image` 类型有值；图片原始像素高 |
| `ImageByteSize` | `long` | 仅 `Image` 类型有值；PNG 文件字节数，用于容量统计 |
| `FilePaths` | `IReadOnlyList<string>` | 仅 `Files` 类型有值；完整路径列表，顺序保持复制时顺序 |
| `Truncated` | `bool` | 文本是否被截断；非文本恒为 `false` |
| `CreatedUtc` | `DateTime` | 首次复制入库时间（UTC） |
| `LastCopiedUtc` | `DateTime` | 最近一次复制时间（UTC），去重置顶与未固定排序依据 |
| `PinnedUtc` | `DateTime?` | 固定时间；`null` 表示未固定 |

约束：

- 单条 `Text` 序列化后不超过 32KB；超过 256KB 的文本不入库；
- `Files` 类型条目不保存文件内容，仅保存路径；
- 同一 `Kind` 下去重键：`Text` 为全文精确匹配（ordinal）；`Files` 为路径列表忽略大小写拼接；`Image` 按内容不做去重。

## history.json 文件格式

```json
{
  "version": 1,
  "entries": [ { "id": "…", "kind": "Text", "text": "…", "preview": "…", "truncated": false,
                 "createdUtc": "2026-09-12T07:00:00Z", "lastCopiedUtc": "2026-09-12T07:00:00Z",
                 "pinnedUtc": null } ]
}
```

- `version`：存储格式版本，当前 `1`；后续结构变更时递增并提供迁移；
- 条目字段按 `ClipboardEntry` 定义序列化，类型无关字段以 `null` 省略；
- 文件写入采用防抖策略，不要求每次复制立即落盘（具体时机由设计文档定义）。

## 默认保留参数（与 REQ 待确认项 1 对应）

| 参数 | 默认值 |
| --- | --- |
| 未固定条目数量上限 | 500 |
| 未固定条目保留期限 | 30 天（按 `LastCopiedUtc`） |
| 图片缓存总容量上限 | 200MB |
| 单条文本存储截断阈值 | 32KB |
| 单条文本入库拒绝阈值 | 256KB |

## 变更记录

| 日期 | 变更 |
| --- | --- |
| 2026-09-12 | 创建草案 |
| 2026-09-12 | 用户决定暂缓本功能，草案挂起；恢复时重新确认后转 `approved` |
