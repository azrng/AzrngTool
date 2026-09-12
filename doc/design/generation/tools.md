---
doc_id: DES-GENERATION-001
doc_type: design
module: generation
feature: tools
status: in-review
last_updated: 2026-09-12
related:
  - doc/requirements/generation/tools.md
---

# 生成与加密工具组设计（DES-GENERATION-001）

> 本文档为依据当前实现整理的回溯基线，待用户确认后转为 approved。

## 需求追溯

- 关联需求：`REQ-GENERATION-001`。
- 覆盖导航分组"生成类工具"下十个工具页的实现模式与关键决策。

## 技术设计

### 共性实现模式

十个工具页遵循同一页面模式，不引入独立服务层：

| 层 | 约定 |
| --- | --- |
| ViewModel | 每页一个 `XxxPageViewModel`（`ViewModelBase` + CommunityToolkit.Mvvm 源生成 `ObservableProperty` / `RelayCommand`） |
| 算法 | 在 ViewModel 内实现，或复用 `Azrng.Core` 已有能力（以各页代码为准） |
| 视图 | `Views/TextHandle/`（GUID、Json Schema、JSON 转 C#、密码生成器）与 `Views/Encrypts/`（HASH、HMAC、AES、DES、SM4、RSA），经 `ViewLocator` 命名约定绑定 |
| 持久化 | 无；输入输出不落盘，无网络请求 |

页面结构统一为：输入区 → 选项区（算法 / 密钥 / 格式）→ 动作按钮 → 输出区 → 复制。

### 各页要点

| 页面 | 要点 |
| --- | --- |
| GUID 生成器 | 批量生成，输出格式化 GUID 列表 |
| HASH / HMAC HASH | 文本摘要与带密钥摘要，算法可选 |
| AES / DES / SM4 | 对称加解密：密钥、IV、模式选项，输出 Base64 / 十六进制 |
| RSA | 密钥对生成、公钥加密、私钥解密 |
| Json Schema 生成 | 解析输入 JSON，生成对应 Schema |
| JSON 转 C# 实体类 | 解析输入 JSON，生成实体类代码 |
| 密码生成器 | 按长度与字符集随机生成 |

### 关键技术决策

1. **不建服务层**：均为无状态即时计算，抽象服务层不带来复用价值；若后续多页共享同一算法实现，再收敛到 `Azrng.Core` 或共享服务。
2. **失败不输出半成品**：加解密失败（密钥不符、数据损坏）明确报错并保留上次成功输出，避免用户误拷乱码结果。
3. **无持久化**：加解密输入输出含敏感数据，页面关闭即丢弃，不写入本地文件。

## 交互与异常

| 场景 | 处理 |
| --- | --- |
| 输入为空 | 提示补全，不执行计算 |
| 密钥长度 / 格式不符 | 提示具体要求 |
| JSON 解析失败 | 提示格式错误位置 |
| 解密失败 | 明确报错，不输出结果 |
| 计算成功 | 输出区更新，支持一键复制 |

## 验证方式

- 已知标准向量对照（如 SHA-256 标准摘要、AES 标准测试向量）验证算法正确性；
- 手动 smoke：各页空输入、非法密钥、往返加解密（加密后再解密还原）路径。
