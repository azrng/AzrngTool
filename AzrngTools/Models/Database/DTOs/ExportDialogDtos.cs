using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AzrngTools.Models.Database.DTOs;

public enum ExportObjectType
{
    Table,
    View,
    Procedure
}

public enum ExportDocumentType
{
    Excel,
    Markdown
}

public partial class ExportObjectItemDto : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _schemaName = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private ExportObjectType _objectType;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isVisible = true;

    public string QualifiedName => string.IsNullOrWhiteSpace(SchemaName)
        ? Name
        : $"{SchemaName}.{Name}";

    public string TypeLabel => ObjectType switch
    {
        ExportObjectType.Table => "表",
        ExportObjectType.View => "视图",
        ExportObjectType.Procedure => "存储过程",
        _ => "对象"
    };
}

public partial class ExportObjectGroupDto : ObservableObject
{
    private bool _isUpdatingSelectionState;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _iconText = string.Empty;

    [ObservableProperty]
    private ExportObjectType _objectType;

    [ObservableProperty]
    private ObservableCollection<ExportObjectItemDto> _items = new();

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private bool? _isSelected;

    public int TotalCount => Items.Count;

    public int VisibleCount => Items.Count(item => item.IsVisible);

    public int SelectedCount => Items.Count(item => item.IsSelected);

    public string CountText => $"{VisibleCount}/{TotalCount}";

    public ExportObjectGroupDto()
    {
        Items.CollectionChanged += OnItemsChanged;
    }

    partial void OnIsSelectedChanged(bool? value)
    {
        if (_isUpdatingSelectionState || value is null)
        {
            return;
        }

        _isUpdatingSelectionState = true;
        try
        {
            foreach (var item in Items)
            {
                item.IsSelected = value.Value;
            }
        }
        finally
        {
            _isUpdatingSelectionState = false;
        }

        RefreshState();
    }

    public void RefreshState()
    {
        foreach (var item in Items)
        {
            item.PropertyChanged -= OnItemPropertyChanged;
            item.PropertyChanged += OnItemPropertyChanged;
        }

        var selectedCount = Items.Count(item => item.IsSelected);
        bool? nextState = selectedCount == 0
            ? false
            : selectedCount == Items.Count
                ? true
                : null;

        _isUpdatingSelectionState = true;
        try
        {
            IsSelected = nextState;
        }
        finally
        {
            _isUpdatingSelectionState = false;
        }

        IsVisible = Items.Any(item => item.IsVisible);
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(VisibleCount));
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(CountText));
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (ExportObjectItemDto item in e.OldItems)
            {
                item.PropertyChanged -= OnItemPropertyChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (ExportObjectItemDto item in e.NewItems)
            {
                item.PropertyChanged += OnItemPropertyChanged;
            }
        }

        RefreshState();
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ExportObjectItemDto.IsSelected) or nameof(ExportObjectItemDto.IsVisible))
        {
            RefreshState();
        }
    }
}

public class ExportDialogResultDto
{
    public string DocumentName { get; set; } = string.Empty;

    public string OutputDirectory { get; set; } = string.Empty;

    public ExportDocumentType DocumentType { get; set; } = ExportDocumentType.Excel;

    public Collection<ExportSelectedObjectDto> SelectedObjects { get; set; } = new();
}

public class ExportSelectedObjectDto
{
    public string Name { get; set; } = string.Empty;

    public string SchemaName { get; set; } = string.Empty;

    public ExportObjectType ObjectType { get; set; }
}
