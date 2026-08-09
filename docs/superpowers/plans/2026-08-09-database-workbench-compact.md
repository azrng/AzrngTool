# 数据库工作台紧凑化实现计划

> **面向 AI 代理的工作者：** 必需子技能：使用 superpowers:subagent-driven-development（推荐）或 superpowers:executing-plans 逐任务实现此计划。步骤使用复选框（`- [ ]`）语法来跟踪进度。

**目标：** 删除数据库工作台中重复的数据表列表，并将工作台和导出向导调整为紧凑、专业的桌面布局。

**架构：** 保持 `DatabaseTree → MainWindowViewModel → TableDetailViewModel` 的既有对象选择链路。`TableDetailView` 不再依据列表状态切换分栏，而是始终以 `TableDetailContent` 填满右侧主工作区；导出窗口只调整 AXAML 样式资源和间距，不改变绑定或命令。

**技术栈：** .NET 10、Avalonia 12、Fluent、Avalonia Headless、xUnit、CommunityToolkit.Mvvm。

---

## 文件结构

- 修改：`AzrngTools.Tests/Views/Database/DatabaseWorkbenchPageViewTests.cs` — 以 Headless 测试锁定“无重复表列表”的视图结果，并沿用现有“更多操作 → 导出”回归测试。
- 修改：`AzrngTools/Views/Database/Workbench/TableDetailView.axaml` — 删除中间对象列表的布局，只保留加载提示和全宽表详情内容。
- 修改：`AzrngTools/Views/Database/DatabaseWorkbenchPageView.axaml` — 收紧连接工具栏和左侧对象浏览器宽度、间距与说明文案。
- 修改：`AzrngTools/Views/Database/Workbench/ExportDialog.axaml` — 用现有设计令牌压缩向导内外边距、区块间距和格式卡片密度。
- 修改：`TASK.md` — 将 T125 更新至阶段 1，记录验证与交付状态。

### 任务 1：用 Headless 测试锁定无重复表列表

**文件：**
- 修改：`AzrngTools.Tests/Views/Database/DatabaseWorkbenchPageViewTests.cs`

- [x] **步骤 1：编写失败的测试**

在现有测试类中加入下列测试；它只检查用户可见的结构，不依赖真实数据库：

```csharp
[Fact]
public async Task Table_detail_view_renders_a_single_full_width_detail_surface_without_the_duplicate_table_list()
{
    await using var session = HeadlessUnitTestSession.StartNew(typeof(WorkbenchTestApplication));
    await session.Dispatch(() =>
    {
        var view = new TableDetailView
        {
            DataContext = new TableDetailViewModel(new DatabaseService())
        };
        var window = new Window { Content = view };
        window.Show();

        Assert.Empty(view.GetVisualDescendants().OfType<DataGrid>());
        Assert.Single(view.GetVisualDescendants().OfType<TableDetailContent>());

        window.Close();
    }, CancellationToken.None);
}
```

- [x] **步骤 2：运行测试验证失败**

运行：

```powershell
dotnet test AzrngTools.Tests/AzrngTools.Tests.csproj --filter "FullyQualifiedName~Table_detail_view_renders_a_single_full_width_detail_surface_without_the_duplicate_table_list" --no-restore
```

预期：失败，`Assert.Empty` 报告仍发现 `DataGrid`；该 `DataGrid` 正是旧的“数据表列表”。

### 任务 2：移除表详情的重复对象列表

**文件：**
- 修改：`AzrngTools/Views/Database/Workbench/TableDetailView.axaml`
- 测试：`AzrngTools.Tests/Views/Database/DatabaseWorkbenchPageViewTests.cs`

- [x] **步骤 1：编写最少实现代码**

将 `Grid.Row="1"` 的三个条件区域替换为单一详情容器，保留顶部加载条和既有 `TableDetailContent` 的空状态：

```xml
<Border Grid.Row="1"
        Classes="workspaceSurface"
        MinWidth="0"
        MinHeight="0">
    <local:TableDetailContent />
</Border>
```

删除旧的 `ShowEmptyState`、`ShowSplitWorkspace` 与 `ShowDetailOnly` 三个 AXAML 区块，以及它们内部的 `DataGrid`、数据表标题和“刷新详情 / 清空”按钮。不要删除 `TableDetailViewModel.Tables`、`SelectedTable` 或加载命令，因为它们仍属于现有对象加载和详情选择链路。

- [x] **步骤 2：运行测试验证通过**

运行：

```powershell
dotnet test AzrngTools.Tests/AzrngTools.Tests.csproj --filter "FullyQualifiedName~Table_detail_view_renders_a_single_full_width_detail_surface_without_the_duplicate_table_list" --no-restore
```

预期：通过；视觉树中无 `DataGrid`，且只有一个 `TableDetailContent`。

### 任务 3：收紧工作台与导出向导的视觉层级

**文件：**
- 修改：`AzrngTools/Views/Database/DatabaseWorkbenchPageView.axaml`
- 修改：`AzrngTools/Views/Database/Workbench/ExportDialog.axaml`
- 测试：`AzrngTools.Tests/Views/Database/DatabaseWorkbenchPageViewTests.cs`

- [x] **步骤 1：调整工作台 AXAML**

在主工作台使用现有令牌完成下列收紧：

```xml
<Grid ColumnDefinitions="Auto,220,220,Auto,Auto,*"
      ColumnSpacing="{StaticResource Space.8}">
...
<Grid.ColumnDefinitions>
    <ColumnDefinition Width="304" />
    <ColumnDefinition Width="12" />
    <ColumnDefinition Width="*" />
</Grid.ColumnDefinitions>
...
<TextBlock Text="选择对象后在右侧查看详情"
           Classes="caption" />
```

将右侧状态胶囊移至新的末列，移除旧的空白列。所有按钮命令、`MenuFlyout`、控件名称和绑定路径保持不变。

- [x] **步骤 2：调整导出向导 AXAML**

以已有令牌替换较大的固定留白，不改变最小尺寸、双栏结构和页脚命令：

```xml
<Border Margin="{StaticResource Thickness.16}"
        Padding="{StaticResource Thickness.16}"
        Classes="exportDialogSurface">
    <Grid RowDefinitions="Auto,*,Auto"
          RowSpacing="{StaticResource Space.12}">
```

将 `exportWizardHeader`、`exportSectionCard`、`exportFormGroup`、`exportDocumentTypeCard`、`exportFooter` 的内边距依次压缩至 `Thickness.16`、`Thickness.14`、`Thickness.12`、`Thickness.12`、`Thickness.12`；将内容网格的 14 / 18 间距替换为 `Space.10` / `Space.12`。

- [x] **步骤 3：运行受影响的 Headless 测试**

运行：

```powershell
dotnet test AzrngTools.Tests/AzrngTools.Tests.csproj --filter "FullyQualifiedName~DatabaseWorkbenchPageViewTests" --no-restore
```

预期：通过；现有测试继续证明“更多操作 → 导出”命令绑定与导出对话框挂载未回归。

### 任务 4：完成回归、构建与交付记录

**文件：**
- 修改：`TASK.md`
- 测试：`AzrngTools.Tests/Views/Database/DatabaseWorkbenchPageViewTests.cs`

- [x] **步骤 1：执行完整数据库相关回归**

运行：

```powershell
dotnet test AzrngTools.Tests/AzrngTools.Tests.csproj --filter "FullyQualifiedName~Database" --no-restore
```

预期：所有筛选到的数据库测试通过，无失败项。

- [x] **步骤 2：执行 Release 构建**

运行：

```powershell
dotnet build AzrngTools/AzrngTools.csproj -c Release --no-restore
```

预期：退出码为 0；若出现既有依赖告警，在交付中记录但不将其归因于本次 AXAML 改动。

- [x] **步骤 3：更新任务状态并提交**

将 T125 更新为 `DONE`，记录已删除重复列表、已完成的 Headless 测试和 Release 构建。仅暂存本任务的 AXAML、测试、`TASK.md` 和本计划，不暂存用户已有的项目文件变更。

```powershell
git add -- AzrngTools.Tests/Views/Database/DatabaseWorkbenchPageViewTests.cs AzrngTools/Views/Database/Workbench/TableDetailView.axaml AzrngTools/Views/Database/DatabaseWorkbenchPageView.axaml AzrngTools/Views/Database/Workbench/ExportDialog.axaml TASK.md
git add -f -- docs/superpowers/plans/2026-08-09-database-workbench-compact.md
git commit -m "style(数据库工作台): 紧凑化对象详情与导出向导"
```

预期：提交只包含 T125 相关文件，未包含用户已有的 `AzrngTools/AzrngTools.csproj` 改动。

## 自检结论

- 规格第 1 至第 9 节分别由任务 1 至任务 4 覆盖：重复栏删除、对象树导航、紧凑布局、导出向导、设计令牌、交互边界、验证和回退均有明确实施点。
- 已扫描计划内容；无未定义的后续工作占位项。
- 测试名称、`TableDetailView`、`TableDetailContent`、`DatabaseWorkbenchPageViewTests` 和既有命令名称与当前代码一致。

## 执行方式

用户已明确要求“开始开发”，本次采用当前会话内联执行：先走任务 1 的红灯，再依次完成任务 2 至任务 4。
