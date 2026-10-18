# 应用壳原型（shell-proto）

> 覆盖范围：主窗口应用壳——UrsaWindow 装饰标题栏（两行标题 + 副标题）、右上标语与主题开关、标题底条 52px + 分隔线、侧边导航（搜索框、统计卡片、分组手风琴、收起/展开）、内容区占位页。
> 不覆盖：具体工具页面、业务逻辑、真实数据。全部为静态模拟数据。

## 运行

```powershell
cd doc/design/prototype/app/shell-proto
dotnet run --project ShellProto.csproj
```

## 还原基准

以主工程当前实现为准（2026-10-18）：

| 基准项 | 来源 |
| --- | --- |
| 布局结构 | `AzrngTools/Views/MainWindow.axaml`（三行：52px 底条、1px 分隔线、内容区；侧栏 248/72 两档） |
| 设计令牌 | `AzrngTools/Styles/DesignTokens.axaml`（应用壳涉及的键同值拷贝至 `Styles/DesignTokens.axaml`） |
| 全局样式 | `AzrngTools/Styles/Global.axaml`（`shell-sidebar`、`shell-panel`、标题、导航文字等摘选） |
| 标题栏高度 52 | `AzrngTools/Styles/WindowDecorations.axaml`（DefaultTitleBarHeight） |
| 导航分组数据 | `AzrngTools/ViewModels/MainWindowViewModel.cs` `CreateMenuBars()`（7 组 33 工具，模拟数据硬编码） |

## 标题栏美化（原型迭代 1）

- 标题底条：纯色 `#F7F8FA` → 纵向淡蓝→白渐变（`#DCEBFF → #F7F8FA`），与首页 Hero 卡片呼应。
- 品牌竖条：标题左侧新增 3×36 圆角竖条（`Brush.Accent.Strong`），标题文字起点左距 56px（竖条 12px + 间距）。
- 标语：裸文本 → 胶囊徽章（淡蓝底 `Brush.Accent.Soft` + 品牌描边 `Brush.Accent.Border`，字号 18→12、Bold→SemiBold）。
- 验证：`shot-1360.png`（默认 1360×860）像素采样——渐变顶底 `(223,237,254)/(245,247,250)`、竖条 `(29,78,216)`、胶囊底 `(220,235,255)` 均与令牌一致。
- 深色主题适配（主工程回写时补齐）：渐变改 `TitleBar.StripGradient` 主题资源键，ThemeDictionaries 分主题定义——明 `#DCEBFF→#F7F8FA`、暗 `#1F2B45→#1C1C1F`；`shot-main-dark.png` 为主工程深色启动实测（顶部 `(31,42,66)`、竖条深色变体 `#7FB3FF`）。

## 主工程回写（2026-10-18 已完成）

渐变、品牌竖条、标语胶囊、标题 56px 缩进与 6px 顶补正已同步至主工程 `AzrngTools/Views/MainWindow.axaml` 与 `Styles/Global.axaml`；取值登记于 `design-system.yaml` `layout.shell.title_bar`。

## 与主工程的差异（原型约束所致）

- 常用入口（主页按钮统计卡片中的 home 图标按钮保留占位，但无跳转逻辑）。
- 搜索只更新计数文案，不过滤导航树。
- 内容区为静态占位页，不接入 ViewLocator 与页面缓存。
- 设计令牌仅拷贝应用壳所需键；确认后回填仍以主工程 `design-system.yaml` 与 `DesignTokens.axaml` 为准。

## 已验证

- `dotnet build` 0 警告 0 错误。
- 1280 / 1440 / 1920 三档宽度窗口截图（`shot-*.png`），侧栏边界、标题底条、分隔线、内容区像素采样与设计令牌一致。
