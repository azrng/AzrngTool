# 2026-04-04 Misc 样式旧 token 收口
## 本次目标
- 清理 `Misc.axaml` 中残留的旧 `Brand/Neutral` 色阶和历史别名资源，统一切回当前主题语义 token

## 核心改动
- 将卡片、侧边栏、标签按钮和概览卡片里残留的 `Brush.Brand.*`、`Brush.Neutral.*` 替换为 `Brush.Panel.*`、`Brush.Nav.*`、`Brush.Accent.*`
- 将 `CardBackground`、`PrimaryColor`、`SidebarSelected`、`TextPrimary`、`BorderLight` 等历史别名替换为当前 `Panel/Text/Accent/Nav/Border` 语义资源
- 顺手统一窗口控制组和概览卡片的底色、边框色与选中前景色，避免在亮暗主题间继续走旧色板

## 修改文件
- `TASK.md`
- `AzrngTools/Assets/Styles/Controls/Misc.axaml`

## 校验情况
- 已执行：`dotnet build AzrngTools.sln`
- 结果：通过，0 warning，0 error

## 风险或遗留项
- 本次只收口了 `Misc.axaml`，仓库内其他页面或控件文件若还残留历史别名 token，需要继续逐文件清理
- `overviewHeroPill`、`overviewIconTile` 里仍保留半透明白色硬编码，后续如果继续做样式纯化，可以单独把这类透明层也抽回语义资源
