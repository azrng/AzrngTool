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
- 数据库工作台：集成 `DbTools / SmartSQL.UI` 子项目，提供连接管理、数据库树浏览、表/视图/存储过程详情与文档导出
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
dotnet publish AzrngTools\AzrngTools.csproj -c Release -r win-x64 --self-contained true -p:PublishAot=false -p:PublishTrimmed=false
```

- 正式发布默认采用：`SelfContained + PublishSingleFile + PublishReadyToRun`
- 当前默认发布策略不启用 `AOT` 和 `Trim`，优先保证桌面运行稳定性

### GitHub Actions 自动发布
1. 将准备好的代码合并到 `main`
2. 仅在正式发布时修改根目录 `VERSION`
3. 推送 `main`
4. 工作流会在 `main` 上检测 `VERSION` 是否变化，或当前版本是否缺少对应的 `v版本号` tag
5. 如果需要补发版本，`main` 工作流会先自动创建并推送对应 tag
6. 当 `v*` tag 被推送，或在 Actions 中手动运行该工作流并选择对应 tag 时，工作流会自动：
   - 执行 `dotnet publish`
   - 生成 `AzrngTools-win-x64-portable.zip`
   - 上传 Actions artifact
   - 创建 GitHub Release
7. 如果已经存在 tag 但 Releases 页面仍为空，可在 Actions 中手动运行 `release-win-x64`，并选择对应 tag 进行补发
8. 如果 `VERSION` 未变化且当前版本 tag 已存在，`main` 工作流会直接跳过发布

## 版本号规则
- 采用三段纯数字版本：`YYYY.M.P`
- `YYYY` 为年份，`M` 为月份，`P` 为当前月份内的正式发布序号
- 版本单一事实来源：根目录 `VERSION`
- GitHub Actions 正式发布时会基于版本号生成带构建信息的 tag，例如：`v2026.4.9-build-20260408-123000-abcd123`
- 客户端显示版本与更新比较统一使用三段主版本号
- 为兼容历史安装包，更新检测仍支持识别旧四段版本：`YYYY.M.D.N`
- 示例：
  - `VERSION`：`2026.4.9`
  - 自动生成 tag：`v2026.4.9-build-20260408-123000-abcd123`
  - 历史兼容版本：`2026.4.8.1221`

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
