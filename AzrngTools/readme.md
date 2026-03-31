# AzrngTools

## 项目简介
AzrngTools 是一个基于 Avalonia 的桌面工具箱应用，聚焦开发与效率场景，提供编码转换、加解密、格式化、文本处理、系统信息等常用工具。

## 当前技术栈
- .NET 10
- Avalonia UI 11.3.x
- Semi.Avalonia
- CommunityToolkit.Mvvm
- Microsoft.Extensions.DependencyInjection
- Azrng.Core

## 样式系统
当前样式资源统一由以下文件管理：
- `AzrngTools/Styles/DesignTokens.axaml`
- `AzrngTools/Styles/Global.axaml`
- `AzrngTools/Themes/*.axaml`

约束说明：
- 被修改过的页面，不再继续新增硬编码的间距、圆角、字号和颜色
- 间距、圆角、字号优先使用 `StaticResource`
- 主题颜色优先使用 `DynamicResource`

## 功能模块
- 编码工具：Base64、URL、Unicode、Hex、JWT、Gzip、繁简转换等
- 加密工具：AES、DES、SM4、RSA、Hash、HMAC
- 格式化工具：JSON、SQL、XML、正则、字数统计、MIME 查询等
- 文本处理：GUID、Markdown、Json Schema、Json 转 C#、密码生成
- 其他工具：Unix 时间戳、翻译
- 设置：硬件信息、关于页、检查更新

## 构建与运行
### 本地构建
```bash
dotnet build AzrngTools.sln -v minimal
```

### 本地发布
```bash
dotnet publish AzrngTools\AzrngTools.csproj -c Release -r win-x64
```

### GitHub Actions 发布
1. 先合并代码到 `main`
2. 更新 `AzrngTools/AzrngTools.csproj` 中的 `Version`
3. 基于 `main` 当前提交创建并推送对应 tag，例如 `v2026.4.1.1`
4. GitHub Actions 仅在 tag 对应提交属于 `main` 分支时执行正式发布
5. 工作流会生成 `AzrngTools-win-x64-portable.zip`，同时上传 Actions artifact 并创建 GitHub Release 资产

## 版本号规则
- 采用四段纯数字版本：`主年.月.日.序号`
- 推荐格式：`YYYY.M.D.N`
- 对应 tag 格式：`vYYYY.M.D.N`
- 同一天第一次发布可用 `N=1`，当天第二次修复发布递增为 `2`、`3`
- 示例：
  - 项目文件版本：`2026.4.1.1`
  - Git tag：`v2026.4.1.1`
  - 同日热修复：`2026.4.1.2`
- 这样做的原因：
  - 与你项目当前日期型版本风格一致
  - 保持 .NET `Version` 可直接比较
  - 关于页检查更新时可以稳定按数字大小判断新旧版本

## 自动更新
- 关于页提供“检查更新”和“立即更新”入口
- 客户端会读取 GitHub Release 的最新版本信息，并下载固定命名的便携版 zip
- 更新包下载完成后，应用会在退出后自动覆盖当前目录并重新启动
- 如果安装目录没有写入权限，请改为手动下载 release zip 覆盖

## 目录说明
```text
AzrngTools/
├── Assets/          # 图标、图片资源
├── Behaviors/       # Avalonia 行为扩展
├── Core/            # 核心算法或底层能力
├── Models/          # 业务模型
├── Services/        # 服务抽象与实现
├── Styles/          # 设计 token 与全局样式
├── Themes/          # 控件主题
├── Utils/           # 工具类与消息服务
├── ViewModels/      # 视图模型
└── Views/           # 页面与窗口
```

## 开发说明
- 当前项目以现有工具箱结构为准，优先渐进改造
- `Ursa.Avalonia` 目前不是默认依赖，仅在复杂导航或对话场景下再评估引入
- 任务状态记录在 `TASK.md`
- 每次开发完成后应补充 `doc/devlog/`
