# AzrngTools

## 项目简介
AzrngTools 是一个基于 Avalonia 的桌面工具箱应用，聚焦开发与效率场景，提供编码转换、加解密、格式化、文本处理、系统信息等常用工具。

## 当前技术栈
- .NET 10
- Avalonia UI 12
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
- 数据库工作台：集成 `DbTools / SmartSQL.UI` 子项目，提供连接管理、数据库树浏览、表/视图/存储过程详情与文档导出
- 网络与接口：提供接口调试工作台，支持请求配置、抽屉式历史侧栏、完整请求 URL 回显、请求中取消与本地历史记录
- 编码工具：Base64、URL、Unicode、Hex、JWT、Gzip、繁简转换等
- 加密工具：AES、DES、SM4、RSA、Hash、HMAC
- 格式化工具：JSON、SQL、XML、正则、字数统计、MIME 查询等
- 文本处理：GUID、Json Schema、Json 转 C#、密码生成
- 其他工具：Unix 时间戳、翻译
- 设置：硬件信息、关于页、检查更新

## 兼容说明
- `Markdown预览` 菜单入口已在 Avalonia 12 升级阶段临时隐藏
- 当前项目仍引用 `Markdown.Avalonia`，该组件尚未完成稳定的 Avalonia 12 兼容验证
- 主程序构建和启动不受影响，后续会在确认稳定方案后恢复该入口

## 构建与运行
### 本地构建
```bash
dotnet build AzrngTools.sln -v minimal
```

### 本地运行
```bash
dotnet run --project AzrngTools\AzrngTools.csproj
```

### PDF 管理依赖
- PDF 管理功能使用项目内本地 DLL 引用：`AzrngTools/Libs/Aspose/Aspose.Pdf.dll`
- 不通过 NuGet 引用 `Aspose.PDF`，更新 Aspose 版本时需替换该 DLL 并完成构建与发布验证
- Aspose 授权文件默认放在应用程序目录，文件名为 `Aspose.Pdf.lic`
- 未放置有效授权文件时，PDF 分割和 Word 导出会以 Aspose 评估模式运行，可能出现评估水印或页数限制

### 本地发布
- 双击根目录 `Publish.bat`，以 Native AOT 方式发布单文件 `AzrngTools.exe` 到 `dist` 目录
- 等价命令：
```bash
dotnet publish AzrngTools\AzrngTools.csproj -c Release -r win-x64 -o dist -p:PublishAot=true
```
- 传 `-p:PublishAot=true` 时 csproj 会联动启用 `PublishTrimmed` 并关闭 `PublishReadyToRun`；本地 Debug / 常规 Release 构建不受影响（仍为 JIT）
- AOT 产物是单个原生 exe，无需运行时自解压；JSON 持久化统一走源生成序列化上下文，XSLT 转换工具在 AOT 版本中不可用（依赖运行时代码生成，页面会给出提示）
- 手动 zip 分发时可参考 CI 命名：`AzrngTools-win-x64-portable.zip`（自动更新按该名称查找更新包）

### GitHub Actions 自动发布
1. 将准备好的代码合并到 `main`
2. 根目录 `VERSION` 默认保持 `auto`，无需手动指定版本号
3. 推送 `main`，或在 Actions 中手动运行 `release-win-x64`
4. 工作流按“基准版本 + 运行序号”生成版本号（对齐 SmartVault，保证随推送严格递增）：
   - 基准取 `VERSION` 的主版本.次版本（如 `1.2`）；内容为 `auto`、为空或不存在时，基准回退为 UTC 日期 `YYYY.M.D`
   - 末段 patch 固定使用 workflow 运行序号 `github.run_number`
5. 工作流会自动：
   - 以 Native AOT 方式执行 `dotnet publish`
   - 生成 `AzrngTools-win-x64-portable.zip`
   - 上传 Actions artifact
   - 创建 GitHub Release，tag 为 `v<版本号>`

## 版本号规则
- 正式发布版本 = `基准.运行序号`，随每次推送严格递增，可直接被客户端更新比较使用
- `VERSION` 默认填写 `auto`：基准回退为 UTC 日期，版本形如 `YYYY.M.D.<run_number>`
- 若需自定义大版本，`VERSION` 填写主版本.次版本（如 `1.2`），发布版本形如 `1.2.<run_number>`
- 本地开发构建未指定版本时，仍按 `Directory.Build.props` 的 UTC 时间版本 `YYYY.M.D.HH` 生成，仅作标识用途
- GitHub Actions 正式发布的 tag 为 `v<版本号>`，例如：`v2026.9.12.1`
- 客户端显示版本与更新比较统一使用可比较的数字版本号
- 为兼容历史安装包，更新检测仍支持识别旧三段 / 四段时间版本：`YYYY.M.D`、`YYYY.M.D.HH`
- 示例：
  - `VERSION`：`auto`
  - 首次推送生成版本：`2026.9.12.1`，tag：`v2026.9.12.1`
  - 再次推送：`2026.9.12.2`（运行序号递增）
  - `VERSION`：`1.2` 时发布版本：`1.2.<run_number>`，tag：`v1.2.<run_number>`

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
├── Modules/         # 并入的子模块项目，当前包含 SmartSQL.UI 数据库工作台
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
