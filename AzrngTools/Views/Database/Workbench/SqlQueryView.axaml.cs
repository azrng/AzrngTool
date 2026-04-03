using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Data;
using AzrngTools.ViewModels.Database;

namespace AzrngTools.Views.Database.Workbench;

public partial class SqlQueryView : UserControl
{
    private SqlQueryViewModel? _viewModel;

    public SqlQueryView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => RebindViewModel();
    }

    private void RebindViewModel()
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.ResultColumns.CollectionChanged -= OnResultColumnsChanged;
        }

        _viewModel = DataContext as SqlQueryViewModel;
        if (_viewModel == null)
        {
            return;
        }

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.ResultColumns.CollectionChanged += OnResultColumnsChanged;

        if (this.FindControl<DataGrid>("ResultGrid") is { } resultGrid)
        {
            resultGrid.ItemsSource = _viewModel.ResultRows;
        }

        RebuildResultGridColumns();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel == null)
        {
            return;
        }

        if (e.PropertyName == nameof(SqlQueryViewModel.ResultColumns))
        {
            _viewModel.ResultColumns.CollectionChanged -= OnResultColumnsChanged;
            _viewModel.ResultColumns.CollectionChanged += OnResultColumnsChanged;
            RebuildResultGridColumns();
        }

        if (e.PropertyName == nameof(SqlQueryViewModel.ResultRows)
            && this.FindControl<DataGrid>("ResultGrid") is { } resultGrid)
        {
            resultGrid.ItemsSource = _viewModel.ResultRows;
        }
    }

    private void OnResultColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildResultGridColumns();
    }

    private void RebuildResultGridColumns()
    {
        if (_viewModel == null || this.FindControl<DataGrid>("ResultGrid") is not { } resultGrid)
        {
            return;
        }

        resultGrid.Columns.Clear();
        for (var index = 0; index < _viewModel.ResultColumns.Count; index++)
        {
            var header = _viewModel.ResultColumns[index];
            resultGrid.Columns.Add(new DataGridTextColumn
            {
                Header = string.IsNullOrWhiteSpace(header) ? $"Column {index + 1}" : header,
                Binding = new Binding($"[{index}]"),
                Width = DataGridLength.Auto
            });
        }

        resultGrid.ItemsSource = _viewModel.ResultRows;
    }

    private void OnFormatSqlClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel == null || string.IsNullOrWhiteSpace(_viewModel.SqlText))
        {
            return;
        }

        var lines = _viewModel.SqlText
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line));

        _viewModel.SqlText = string.Join(System.Environment.NewLine, lines);
    }
}
