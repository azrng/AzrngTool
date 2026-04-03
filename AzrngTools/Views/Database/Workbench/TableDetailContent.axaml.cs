using Avalonia.Controls;
using Avalonia.Interactivity;
using AzrngTools.Models.Database;
using AzrngTools.ViewModels.Database;

namespace AzrngTools.Views.Database.Workbench;

public partial class TableDetailContent : UserControl
{
    private Button? _tabColumnsButton;
    private Button? _tabIndexesButton;
    private Button? _tabDdlButton;

    public TableDetailContent()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        _tabColumnsButton = this.FindControl<Button>("TabColumnsButton");
        _tabIndexesButton = this.FindControl<Button>("TabIndexesButton");
        _tabDdlButton = this.FindControl<Button>("TabDdlButton");

        if (DataContext is TableDetailViewModel vm)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;
            UpdateTabClasses(vm.SelectedTabIndex);
        }

        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is TableDetailViewModel vm)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;
            UpdateTabClasses(vm.SelectedTabIndex);
        }
    }

    private async void OnEditTableCommentClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TableDetailViewModel viewModel || viewModel.SelectedTable == null)
        {
            return;
        }

        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var dialogViewModel = new CommentEditorDialogViewModel(viewModel.SaveTableCommentAsync)
        {
            DialogTitle = "编辑表备注",
            PrimaryLabel = "表名",
            PrimaryValue = BuildQualifiedTableName(viewModel, viewModel.SelectedTable),
            OriginalComment = viewModel.TableComment ?? string.Empty,
            EditableComment = viewModel.TableComment ?? string.Empty,
            HintText = "保存后会立即写回数据库，并同步更新当前表的备注信息。"
        };

        var dialog = new CommentEditorDialog(dialogViewModel)
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        await dialog.ShowDialog(owner);
    }

    private async void OnEditColumnCommentClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TableDetailViewModel viewModel ||
            viewModel.SelectedTable == null ||
            sender is not Button { Tag: ColumnModel column })
        {
            return;
        }

        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var dialogViewModel = new CommentEditorDialogViewModel(comment => viewModel.SaveColumnCommentAsync(column, comment))
        {
            DialogTitle = "编辑字段备注",
            PrimaryLabel = "字段",
            PrimaryValue = column.Name,
            ShowSecondaryInfo = true,
            SecondaryLabel = "所属表",
            SecondaryValue = BuildQualifiedTableName(viewModel, viewModel.SelectedTable),
            OriginalComment = column.Comment ?? string.Empty,
            EditableComment = column.Comment ?? string.Empty,
            HintText = "保存后会立即写回数据库，并同步更新当前字段备注。"
        };

        var dialog = new CommentEditorDialog(dialogViewModel)
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        await dialog.ShowDialog(owner);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TableDetailViewModel.SelectedTabIndex)
            && DataContext is TableDetailViewModel vm)
        {
            UpdateTabClasses(vm.SelectedTabIndex);
        }
    }

    private void UpdateTabClasses(int selectedIndex)
    {
        RemoveTabActive(_tabColumnsButton);
        RemoveTabActive(_tabIndexesButton);
        RemoveTabActive(_tabDdlButton);

        var activeButton = selectedIndex switch
        {
            0 => _tabColumnsButton,
            1 => _tabIndexesButton,
            2 => _tabDdlButton,
            _ => _tabColumnsButton
        };

        AddTabActive(activeButton);
    }

    private static void RemoveTabActive(Button? button)
    {
        if (button != null && button.Classes.Contains("tabActive"))
        {
            button.Classes.Remove("tabActive");
        }
    }

    private static void AddTabActive(Button? button)
    {
        button?.Classes.Add("tabActive");
    }

    private static string BuildQualifiedTableName(TableDetailViewModel viewModel, TableModel table)
    {
        var schemaName = !string.IsNullOrWhiteSpace(table.Schema)
            ? table.Schema
            : viewModel.CurrentConnection?.Database;

        return string.IsNullOrWhiteSpace(schemaName)
            ? table.Name
            : $"{schemaName}.{table.Name}";
    }
}
