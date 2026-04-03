# 2026-04-01 Tag 触发 Release 补发修复

## 本次目标
- 修复“仓库已有版本 tag，但 Releases 页面仍为空”时无法自动补发 GitHub Release 的问题

## 核心改动
- 更新 `.github/workflows/release.yml`
- 将发布流程拆分为两段：
  - `main` 推送时负责检测版本并补建 `v版本号` tag
  - `v*` tag 推送或手动触发时负责执行发布、打包与创建 GitHub Release
- 增加 `workflow_dispatch`，支持在 Actions 中选择已有 tag 手动补发 Release
- 更新 `README.md`，补充 tag 与 release 的触发关系，以及已有 tag 的补发方式

## 修改文件
- `.github/workflows/release.yml`
- `README.md`
- `TASK.md`
- `doc/devlog/2026-04-01-Tag触发Release补发修复.md`

## 校验情况
- 已执行：`dotnet build AzrngTools.sln -v minimal`
- 结果：通过，0 warning / 0 error
- 已执行：`git diff --check`
- 结果：未发现 diff 格式错误，仅提示仓库工作区将在后续由 Git 按设置处理 LF/CRLF
- 未执行：GitHub Actions 远端 tag / workflow_dispatch 实跑校验
- 原因：需要推送到 GitHub 后由远端环境触发

## 风险或遗留项
- 当前补发依赖在 Actions 页面手动选择正确的 tag 运行工作流
- GitHub Release 的最终创建结果仍需以远端 Actions 运行日志为准
