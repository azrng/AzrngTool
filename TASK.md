# 任务清单（TASK）

> 本文件只维护当前仍需关注的任务与最近 5 条已完成任务。
> 规则解释与阶段门控仍以 `AGENTS.md` 为准。

---

## 活跃任务

当前无活跃任务。

---

## 最近完成

| 任务 ID | 任务名称 | 任务状态 | 最近更新时间 |
| ------- | -------- | -------- | ------------ |
| T025 | 接口调试页修复：窗口高度不足时响应结果区被请求配置卡（Body 编辑器 MinHeight=300）挤压至 0 且无滚动兜底。左列改为 Auto,*,* 星号分栏（配置卡与响应区平分剩余空间），Params/Headers/表单列表与 Body 编辑器改为内部滚动（列表包 ScrollViewer、编辑器 MinHeight=0），配置卡内网格 Auto,Auto 改 Auto,* 让 TabControl 与编辑器撑满卡片份额（修复编辑器缩成单行、卡片底部留白） | DONE | 2026-09-15 |
| T024 | 发布链路回退 JIT 自包含单文件（Publish.bat/CI 去 AOT 参数，README 与架构文档同步）并新增 AOT 暂缓恢复指南 doc/design/publish-aot.md；修复 UrsaWindow 标题栏悬浮不占布局导致的内容顶到窗口头部的遮挡，并将 MainWindow 内容改为 32px 标题底条 + 分隔线 + 内容区三行布局（标题区底色随明暗主题切换，消除与内容融为一体的漂浮感）；修复 Toast/通知贴窗口顶被裁剪（Ursa WindowToastManager 与两处 WindowNotificationManager 加 Margin 下移避开标题栏）；发布脚本拆分为自包含 Publish.bat 与依赖框架 Publish-FrameworkDependent.bat（内容改纯 ASCII 修复 UTF-8 中文被 cmd/GBK 解析拆断的问题）；窗口圆角经实验后按用户决定不做 | DONE | 2026-09-15 |
| T023 | 自定义组件替换为 UI 库组件：MainWindow 换 UrsaWindow 托管标题栏与窗口按钮、ThemeToggleButton 替代弃用主题开关（偏好持久化改由变体事件驱动）、3 个转换器换内置 BoolConverters/ObjectConverters、DatabaseTree 加载遮罩换 LoadingContainer、DatabaseTypeSelector 去除 code-behind 事件转发并修副标题文案、两处搜索框补 clearButton、删除死主题文件与 windowControl 样式 | DONE | 2026-09-13 |
| T022 | 性能问题修复第一轮（正则超时与后台化、通知管理器泄漏、JWT 解析防抖、硬件采集异步化、搜索防抖、大文本后台化、导出限流、DI 扫描合并等） | DONE | 2026-09-13 |
| T021 | 工具页统一布局 P1：全量迁移 31 页并实机回归 | DONE | 2026-09-12 |

---
