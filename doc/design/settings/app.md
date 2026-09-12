---
doc_id: DES-SETTINGS-001
doc_type: design
module: settings
feature: app
status: in-review
last_updated: 2026-09-12
related:
  - doc/requirements/settings/app.md
  - doc/design/architecture.md
---

# 设置与应用管理设计（DES-SETTINGS-001）

> 本文档为依据当前实现整理的回溯基线，待用户确认后转为 approved。更新与发布链路的跨模块描述见 `DES-ARCH-001`，本文聚焦设置模块内部设计。

## 需求追溯

- 关联需求：`REQ-SETTINGS-001`。
- 覆盖：硬件信息页、关于页与更新链路、主题偏好、侧栏常用入口推荐。
- 不覆盖：多语言、设置中心页等范围外能力。

## 契约引用

- 版本与更新数据结构以 `Models/AppUpdateInfo.cs` 等模型类型为最终事实来源；硬件快照以 `Models/HardwareInfoSnapshot.cs` 为准。

## 技术设计

### 模块职责

| 模块 | 生命周期 | 职责 |
| --- | --- | --- |
| `HardwarePageViewModel` | 页面级 | 硬件信息页编排与展示 |
| `HardwareInfoCacheService` | Singleton | 硬件快照本地缓存：构造时加载缓存文件，避免重复采集；必要时重新采集并回写 |
| `AboutPageViewModel` | 页面级 | 版本展示、检查更新与立即更新入口 |
| `AppUpdateService` | Singleton | 访问 GitHub Releases、下载便携版更新包、数字版本比较（兼容旧三段 / 四段版本） |
| `AppUpdateCoordinatorService` | Singleton | 更新编排：启动自动检查、操作信号量锁、版本信息本地缓存、下载状态管理、状态广播 |
| `ApplicationRuntimeService` | Singleton | 应用退出、覆盖安装与重启 |
| `AppInfoService` | Singleton | 当前版本与仓库信息 |
| `ThemePreferenceService` | Singleton | 亮暗主题偏好：`%LocalAppData%\AzrngTools\theme-settings.json` 持久化，启动时加载请求的主题变体 |
| `ToolUsageStatsService` | Singleton | 工具使用统计与常用入口评分 |

### 更新编排状态流

```text
启动后台检查任务 / 关于页手动检查
        ↓（信号量锁防并发）
AppUpdateService 获取最新版本并比较
        ├── 无新版本 → 提示已是最新
        └── 有新版本 → 缓存版本信息 → 下载 portable zip
                ├── 下载失败 → 记录失败版本，避免反复自动重试，可手动重试
                └── 下载完成 → 提示应用更新（MessageService 广播状态）
                        ↓ 用户确认
        ApplicationRuntimeService：退出 → 覆盖安装目录 → 重启
```

### 常用入口评分

`ToolUsageStatsService` 每次工具打开累计 `UseCount` 并更新 `LastUsedUtc`，评分公式：

- 频率分 = `log2(UseCount + 1)`（平滑，避免高频工具无限放大）；
- 最近分 = `e^(-天数 / 7)`（一周量级的衰减）；
- 最终分 = 频率分 × 0.75 + 最近分 × 0.25。

侧栏常用入口按最终分取前 3 个；统计数据经 2 秒防抖落盘到用户目录 JSON。

### 关键技术决策

1. **自动检查与手动检查共用编排服务**：启动检查在后台任务执行，不阻塞主界面；两者结果统一进缓存与广播，避免两套状态。
2. **下载失败记忆**：记录失败版本号，自动链路不反复重试同一版本，手动重试不受限。
3. **偏好与统计均 fail-soft**：`theme-settings.json` 或统计文件损坏时按默认值重建，不阻塞应用启动。

## 交互与异常

| 场景 | 处理 |
| --- | --- |
| 已是最新版本 | 明确提示，不提供更新动作 |
| 检查 / 下载失败 | 展示可读原因，支持手动重试 |
| 安装目录不可写 | 引导手动下载 Release 压缩包覆盖 |
| 硬件采集失败 / 缓存损坏 | 可重新采集，页面不空白阻塞 |
| 主题文件损坏 | 按默认主题启动 |

## 验证方式

- 手动 smoke：主题切换后重启保持；关于页检查更新 → 下载 → 退出覆盖 → 重启的完整链路；断网时检查更新的失败提示。
- 回归点：启动自动检查不产生可感知启动延迟；更新操作期间重复触发不并发（信号量锁）。
