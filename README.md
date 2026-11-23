# AzrngTools

基于 Avalonia 的 Windows 桌面工具箱：文档转换、编码转换、加解密、格式化、文本处理等能力全部本地完成，数据不出本机。

## 技术栈
- .NET 10（net10.0-windows）
- Avalonia UI 12 + Semi.Avalonia 主题（Ursa 窗口壳与部分控件）
- CommunityToolkit.Mvvm、Microsoft.Extensions.DependencyInjection
- Azrng.Core（结果包装、依赖注入标记接口、帮助类）

## 功能模块
- 文档工具：PDF 管理（页码分割、导出 Word）、Word 转 Markdown（标题层级 / 表格 / 列表 / 超链接，图片导出到同名 `.assets` 目录）
- 网络与接口：接口调试工作台（请求配置、历史侧栏、完整 URL 回显、请求取消）
- 生成类工具：GUID、HASH、HMAC、AES、DES、SM4、RSA、Json Schema、JSON 转 C# 实体类、密码生成器
- 编码/解码类工具：BASE64 文本、Base64 转图片、URL、Unicode、十六进制、繁简体、JWT 解码、Gzip、组合编码解码工作台
- 格式化类工具：文本压缩、JSON 格式化、SQL 操作、XML 转 HTML、JSON/YAML/XML/ENV 互转、正则测试、字数统计、人民币大写、MIME 查询
- 其他工具：Unix 时间戳、翻译
- 设置：关于页、检查更新与自动更新（硬件信息页已随依赖精简暂时隐藏）

## 兼容说明
- `Markdown预览` 菜单入口临时隐藏：`Markdown.Avalonia` 尚未完成 Avalonia 12 兼容验证，代码保留，主程序构建与启动不受影响。

## 环境要求
- Windows x64
- .NET 10 SDK（`global.json` 锁定 10.0.100，rollForward latestFeature）

## 快速开始
```bash
dotnet build AzrngTools.slnx -v minimal
dotnet run --project AzrngTools\AzrngTools.csproj
```

本地发布（输出单文件 `AzrngTools.exe`）：
- `Publish.bat`：自包含发布到 `dist`，目标机器无需安装 .NET
- `Publish-FrameworkDependent.bat`：依赖框架发布到 `dist-fdd`，体积更小，需目标机器安装 .NET 10 桌面运行时

## 版本与发布
- CI：推送 `main` 触发 GitHub Actions `release-win-x64`，自包含发布并打包 `AzrngTools-win-x64-portable.zip`，创建 GitHub Release（tag `v<版本号>`）。
- 版本号 = 基准 + 运行序号：根目录 `VERSION` 为 `auto` 时基准取 UTC 日期，形如 `2026.9.12.1`；填 `1.2` 时形如 `1.2.<run_number>`；随推送严格递增。
- Native AOT 已暂缓，恢复步骤见 `doc/design/publish-aot.md`。

## 使用说明
安装 release zip 解压即用；关于页「检查更新」从 GitHub Releases 拉取最新便携版 zip，退出后自动覆盖并重启。安装目录无写入权限时请手动下载覆盖。

## 项目结构
```text
AzrngTools/
├── Assets/          # 图标、图片资源
├── Behaviors/       # Avalonia 行为扩展
├── Converters/      # 值转换器
├── Core/            # 核心算法或底层能力
├── Extensions/      # DI 注册等扩展方法
├── Models/          # 业务模型（按业务域分子目录）
├── Services/        # 服务抽象与实现（接口与实现同目录，按业务域分子目录）
├── Styles/          # 设计 token 与全局样式
├── Utils/           # 工具类与消息服务
├── ViewModels/      # 视图模型（按业务域分子目录）
└── Views/           # 页面与窗口（与 ViewModel 按业务域一一对应）
```

## 相关文档
- `doc/index.md`：需求（REQ-）/ 设计（DES-）/ 契约（CON-）文档总索引
- `doc/design/architecture.md`：跨模块架构、更新与发布链路
- `design-system.yaml`：设计系统唯一事实来源（样式 token 与约束）
- `TASK.md`：活动任务记录
- `AGENTS.md`：AI 协作规则入口
