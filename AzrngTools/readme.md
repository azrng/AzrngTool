# AzrngTools

## 项目简介
AzrngTools 是一个基于 Avalonia 的桌面工具箱应用，聚焦开发与效率场景，提供编码转换、加解密、格式化、文本处理、系统信息等常用工具。

## 当前技术栈
- Avalonia UI 11.3.x
- .NET 10
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
- 其他工具：Unix 时间戳、翻译、今日新闻
- 设置：硬件信息、关于页

## 构建与运行
### 本地构建
```bash
dotnet build AzrngTools.sln -v minimal
```

### Release 发布
```bash
dotnet publish AzrngTools\AzrngTools.csproj -c Release
```

## 目录说明
```text
AzrngTools/
├── Assets/          # 图标、图片资源
├── Behaviors/       # Avalonia 行为扩展
├── Core/            # 核心算法或底层能力
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
