# 项目经验记录

## [LRN-20260808-001] correction

**Logged**: 2026-08-08T00:00:00+08:00
**Priority**: high
**Status**: resolved
**Area**: frontend

### Summary
数据库连接对话框的数据库类型切换属于完整状态链，不能只验证主工作台入口。

### Details
用户反馈在“新建连接 → 选择 PostgreSQL”后界面没有反应，说明数据库类型选择、表单可见性、类型绑定或命令 CanExecute 之间可能存在断链。后续数据库模块改动必须把连接对话框及其类型切换作为独立回归路径检查。

### Suggested Action
追踪 `ConnectionDialogViewModel`、`DatabaseTypeSelector`、`ConnectionConfigForm` 与保存命令的完整绑定链，补 PostgreSQL 选择和表单状态回归测试。

### Metadata
- Source: user_feedback
- Related Files: AzrngTools/ViewModels/Database/ConnectionDialogViewModel.cs; AzrngTools/Views/Database/Workbench/DatabaseTypeSelector.axaml; AzrngTools/Views/Database/Workbench/ConnectionConfigForm.axaml
- Tags: database, connection-dialog, postgresql, regression

### Resolution
- **Resolved**: 2026-08-08T00:00:00+08:00
- **Notes**: 将连接类型卡片命令改为绑定连接对话框自身的 `MainWindow.DataContext`，修复重复打开对话框后事件不再触发的问题；保存后立即连接不再重复持久化或误判自身同名，补充新建 PostgreSQL、保存与连接流程测试；数据库相关测试 72 项通过。

---

## [LRN-20260808-002] correction

**Logged**: 2026-08-08T00:00:00+08:00
**Priority**: high
**Status**: resolved
**Area**: frontend

### Summary
弹出式菜单不能依赖宿主控件的隐式 `DataContext` 继承来执行工作台命令。

### Details
用户反馈“更多操作 → 导出文档”点击无反应。`MenuFlyout` 在独立弹出层中显示，原菜单项仅绑定 `OpenExportDialogCommand`，运行时无法可靠继承工作台的 ViewModel，因此命令未执行。

### Suggested Action
所有工作台弹出菜单命令均显式绑定命名根控件的 `DataContext`；新增或重构 Flyout 时，将菜单命令作为独立回归路径验证。

### Metadata
- Source: user_feedback
- Related Files: AzrngTools/Views/Database/DatabaseWorkbenchPageView.axaml
- Tags: database, menu-flyout, binding, command, regression
- See Also: LRN-20260808-001

### Resolution
- **Resolved**: 2026-08-08T00:00:00+08:00
- **Notes**: 将导出文档、pg_dump 导出和生成代码菜单均显式绑定到 `WorkbenchPage.DataContext`；Release 构建与数据库相关 72 项测试通过。

---
