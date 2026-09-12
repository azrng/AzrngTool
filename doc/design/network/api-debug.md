---
doc_id: DES-NETWORK-001
doc_type: design
module: network
feature: api-debug
status: in-review
last_updated: 2026-09-12
related:
  - doc/requirements/network/api-debug.md
---

# 接口调试设计（DES-NETWORK-001）

> 本文档为依据当前实现整理的回溯基线，待用户确认后转为 approved。

## 需求追溯

- 关联需求：`REQ-NETWORK-001`。
- 本次覆盖：单页接口调试全流程（配置、发送、取消、响应展示、完整 URL 回显、本地历史）。
- 不覆盖：环境变量、脚本断言、Mock 等需求中列出的范围外能力。

## 契约引用

- 请求与历史的数据结构以 `Models/Network/ApiRequestModels.cs` 为最终事实来源，本文不复制完整字段表。

## 技术设计

### 模块职责

| 模块 | 生命周期 | 职责 |
| --- | --- | --- |
| `ApiRequestPageViewModel` | 页面级 | 请求配置状态、发送 / 取消命令、响应回填、历史侧栏编排 |
| `ApiRequestSupportViewModels` | 页面级 | 参数行、请求头、历史条目等子模型 |
| `ApiRequestExecutionService` | Transient | 组装并执行 HTTP 请求：经 `IHttpClientFactory` 创建客户端，`SendAsync(请求快照, CancellationToken)` 返回执行结果；负责拼接完整 URL 供回显 |
| `ApiRequestStoreService` | Singleton | 本地历史存取：读写用户目录 JSON 文件，互斥锁串行化，条目上限 50 条，按时间倒序返回 |

### 数据流向

```text
页面表单（方法 / URL / 参数 / 头 / 体）
        ↓ 发送命令
ApiRequestExecutionService.SendAsync(快照, CancellationToken)
        ↓ 执行结果（状态码 / 耗时 / 响应体 / 错误）
ApiRequestPageViewModel 回填响应区
        ↓ 请求完成
ApiRequestStoreService 追加历史 → 本地 JSON 落盘
```

### 状态变化

- 请求：`空闲 → 请求中（可取消）→ 完成 | 失败 | 已取消`；任何终态都回到可编辑状态。
- 历史侧栏：`关闭 ⇄ 打开（抽屉）`；打开时读取最新历史。

### 页面结构（现状示意）

```text
┌──────────────────────────────────────────────────────┐
│ [方法 ▾] [URL....................................] [发送] │
│ 完整 URL 回显：https://…?a=1&b=2                        │
├──────────────────────────────────────────────────────┤
│ [Params] [Headers] [Body]   （参数行编辑区）             │
├──────────────────────────────────────────────────────┤
│ 响应：状态码 · 耗时                                     │
│ 响应体…                                               │
└────────────────────────────────────────── [历史 ▸] ───┘
```

历史以抽屉侧栏从边缘展开，不打断请求编辑区。

## 交互与异常

| 场景 | 处理 |
| --- | --- |
| URL 为空 / 非法 | 提示原因，不发起请求 |
| 网络错误 / 超时 | 响应区展示错误类型与原因 |
| 请求中取消 | 经 `CancellationToken` 终止，回到可编辑状态 |
| 非 2xx 响应 | 按正常响应展示，不作为异常打断 |
| 历史超过 50 条 | 淘汰最旧条目 |
| 历史文件损坏 | 从空历史重建，不阻塞页面 |

历史复用：选择历史条目后将请求快照回填表单，用户可修改后再次发送。

## 验证方式

- 手动 smoke（Windows）：GET / POST 请求、参数与请求头生效性（对照完整 URL 回显）、请求中取消、断网错误提示、历史回填重发。
- 回归点：历史文件损坏后页面可正常打开；上限淘汰不报错。
