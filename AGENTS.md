# AGENTS.md

## 项目定位
`AzrngTools` 是一个基于 Avalonia 的桌面工具箱应用，面向开发与日常效率场景，当前重点是：
- 保持工具页可直接操作，不做纯演示交互
- 在现有结构上持续迭代，不为追求“理想架构”强行推倒重来
- 逐步把样式、依赖注入、服务抽象迁回统一规范

## 规则文件分工
- `AGENTS.md` / `CLAUDE.md` / `GEMINI.md`：统一 AI 工作规则，内容保持同步
- `design-system.yaml`：设计 token、布局和样式约束的单一事实来源
- `TASK.md`：唯一任务状态记录文件
- `doc/devlog/`：每次开发完成后的简短记录

## 当前技术栈
- `.NET 10`
- `Avalonia 11.3.x`
- `Semi.Avalonia`
- `CommunityToolkit.Mvvm`
- `Microsoft.Extensions.DependencyInjection`
- `Azrng.Core`
- `HotAvalonia` 仅用于调试期热重载

### 关于 Ursa.Avalonia
- `Ursa.Avalonia` 在本项目中是可选方案，不是默认前置依赖
- 只有当页面明确需要更复杂的导航、对话框、抽屉、分步流程或企业级容器时，才评估引入
- 若引入，必须保证视觉风格与现有 `Semi.Avalonia + Fluent` 体系一致，不允许形成第二套割裂的视觉语言

## 当前推荐架构
优先遵循当前项目的真实结构，而不是套用其他仓库模板：

`View -> ViewModel -> Service -> Utils/Model`

补充约定：
- 新增纯本地算法类工具时，可直接走 `ViewModel + Service/Utils`
- 只有在出现持久化、本地数据库、远端 API 聚合、缓存或复杂 IO 场景时，才新增 `Repository` / `Data` 层
- 不为了“看起来规范”强行引入 SQLite、Dapper、迁移脚本
- 如果仓库里已有真实实现，以现状为准，优先渐进改造

## 样式系统
### 单一事实来源
- `AzrngTools/Styles/DesignTokens.axaml`
- `AzrngTools/Styles/Global.axaml`
- `AzrngTools/Themes/*.axaml`
- `design-system.yaml`

### 强制规则
- 所有被修改过的 XAML 页面，禁止继续新增硬编码的 `Margin`、`Padding`、`CornerRadius`、`FontSize`、颜色值
- 间距、圆角、字号优先使用 `{StaticResource ...}`
- 主题相关颜色优先使用 `{DynamicResource ...}`
- 公共控件视觉规则放在样式表中，不写在控件属性里
- 允许保留少量与功能强相关的 `Width` / `Height`：如预览区、图像尺寸、窗口初始尺寸、编辑器区域

### 迁移原则
- 先补资源字典，再迁移页面
- 先迁移主壳、设置页、最近改动页，再逐步覆盖历史页面
- 触达页面时顺手消除样式硬编码，不把问题继续向后滚

## MVVM 与依赖注入
- 所有 ViewModel 继承 `ViewModelBase`
- 属性使用 `[ObservableProperty]`
- 命令使用 `[RelayCommand]`
- 优先构造函数注入依赖
- 新代码不要在 View 中直接 `new ViewModel()`
- 新代码不要在 ViewModel 中直接 `new HttpClient()`、直接操作 View、或写窗口行为逻辑
- Code-behind 只允许保留窗口拖拽、最小化、附着视觉树、文件选择器等 UI 平台逻辑

## 消息与服务
- 跨 ViewModel 或跨页面提示，优先通过服务或消息抽象处理
- 新代码优先使用封装后的 `IMessageService`，避免在业务代码里到处直接写 `WeakReferenceMessenger.Default`
- 外部请求、系统 API、剪贴板、文件对话框等优先放到 `Service` / `Utils` 抽象中

## 功能开发原则
- 先理解当前工具页的真实输入、输出与用户流程，再动手改
- 优先复用已有页面结构、主题资源和帮助类
- 不做与当前任务无关的重构
- 所有按钮必须可交互，行为要真实生效
- 对外部接口失败、空结果、异常提示，至少要给出用户可见反馈

## 任务管理
`TASK.md` 是唯一任务记录文件。

### 状态
- `TODO`
- `DOING`
- `BLOCKED`
- `REVIEW`
- `DONE`

### 执行要求
1. 开始改动前先检查 `TASK.md`
2. 如果没有对应任务，新增最小记录
3. 开始做时改为 `DOING`
4. 做完待确认改为 `REVIEW`
5. 交付完成改为 `DONE`
6. 无法继续时标记 `BLOCKED` 并写明原因

## 验证要求
- 代码、配置、样式有改动时，默认至少执行一次 `dotnet build`
- 若改动影响工具逻辑，优先补充或更新测试；若仓库暂时没有测试工程，需在最终说明中明确风险
- 若改动影响 UI，至少说明已检查的页面或入口
- 无法执行的验证项必须说明原因、影响范围和建议下一步

## 文档更新
以下变化必须同步更新文档：
- 技术栈版本
- 构建命令
- 样式系统结构
- AI 规则或任务流转方式
- 页面使用方式或核心功能入口

每次开发完成后，需在 `doc/devlog/` 增加一份简短记录，内容至少包含：
- 本次目标
- 核心改动
- 修改文件
- 校验情况
- 风险或遗留项

## Git 要求
- 修改完成后应提交代码
- 提交信息优先使用 Conventional Commits
- 禁止使用 `git commit --no-verify`
- 禁止重置或覆盖用户现有改动

## 禁止事项
- 禁止把别的项目规则原封不动套进当前仓库
- 禁止新增与现有样式系统并行的第二套 token/主题体系
- 禁止在 Code-behind 中写业务逻辑
- 禁止在 ViewModel 中直接操作具体控件
- 禁止为了形式统一而强行引入仓库当前根本没用到的数据库或中间层
- 禁止跳过验证却不说明

## 交付输出要求
最终输出优先使用中文，并至少说明：
- 本次改了什么
- 核心实现方式
- 修改了哪些文件
- 已执行和未执行的校验
- 当前任务状态
- 风险、阻塞或假设
- 是否已更新 `doc/devlog/`
