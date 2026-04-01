# 2026-04-01 main 提交自动 tag 并发布 release

## 本次目标
- 参考 `DbTools` 的工作流模式，实现 `main` 分支每次提交都自动打 tag 并发布 GitHub Release

## 核心改动
- 重写 `.github/workflows/release.yml`
- 改为在单次 `main` push 工作流中直接生成唯一 tag、打包 zip、上传 artifact 并创建 GitHub Release
- 不再依赖“先创建 tag，再等待 tag 触发第二次工作流”的链路
- 发布 tag 改为带时间戳和短 SHA 的唯一构建标识
- 发布时显式写入 `InformationalVersion=版本号+短SHA`
- 更新 `AppInfoService` 与 `AppUpdateService`，支持识别带构建标识的 release，并在相同版本号下比较构建 ID

## 修改文件
- `.github/workflows/release.yml`
- `AzrngTools/Services/IAppInfoService.cs`
- `AzrngTools/Services/AppInfoService.cs`
- `AzrngTools/Services/AppUpdateService.cs`
- `TASK.md`
- `doc/devlog/2026-04-01-main提交自动tag并发布release.md`

## 校验情况
- 已执行：`dotnet build AzrngTools.sln -v minimal`
- 结果：通过，0 warning / 0 error
- 未执行：GitHub Actions 远端自动 tag / release 校验
- 原因：需要推送到 `main` 后在 GitHub Actions 中确认 Release 页面结果

## 风险或遗留项
- 当前客户端仍以 GitHub Release latest 为更新源，因此是否能检查到更新仍取决于远端 Release 是否成功创建
- 若后续需要区分“正式版本”和“每次 main 构建版”，可能还需要再引入 prerelease 或发布频道概念
