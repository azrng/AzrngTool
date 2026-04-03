using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.Input;
using AzrngTools.Models.Database;

namespace AzrngTools.Controls.Database;

public partial class DatabaseTree : UserControl
{
    public TreeNodeItem? RootNode
    {
        get => GetValue(RootNodeProperty);
        set => SetValue(RootNodeProperty, value);
    }

    public static readonly StyledProperty<TreeNodeItem?> RootNodeProperty =
        AvaloniaProperty.Register<DatabaseTree, TreeNodeItem?>(
            nameof(RootNode),
            defaultValue: null,
            inherits: false);

    public TreeNodeItem? SelectedNode
    {
        get => GetValue(SelectedNodeProperty);
        set => SetValue(SelectedNodeProperty, value);
    }

    public static readonly StyledProperty<TreeNodeItem?> SelectedNodeProperty =
        AvaloniaProperty.Register<DatabaseTree, TreeNodeItem?>(
            nameof(SelectedNode),
            defaultValue: null,
            inherits: false);

    public string SearchText
    {
        get => GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public static readonly StyledProperty<string> SearchTextProperty =
        AvaloniaProperty.Register<DatabaseTree, string>(
            nameof(SearchText),
            defaultValue: string.Empty,
            inherits: false);

    public bool IsLoading
    {
        get => GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<DatabaseTree, bool>(
            nameof(IsLoading),
            defaultValue: false,
            inherits: false);

    public string? LoadingText
    {
        get => GetValue(LoadingTextProperty);
        set => SetValue(LoadingTextProperty, value);
    }

    public static readonly StyledProperty<string?> LoadingTextProperty =
        AvaloniaProperty.Register<DatabaseTree, string?>(
            nameof(LoadingText),
            defaultValue: null,
            inherits: false);

    public ObservableCollection<TreeNodeItem> FilteredChildren { get; } = new();

    public RelayCommand? SearchCommand { get; private set; }

    public RelayCommand? ClearSearchCommand { get; private set; }

    public event EventHandler<TreeNodeItem?>? NodeSelected;

    public event EventHandler<TreeNodeItem?>? NodeDoubleClicked;

    public DatabaseTree()
    {
        InitializeComponent();
        SetupEventHandlers();
        SetupCommands();
    }

    private void SetupCommands()
    {
        SearchCommand = new RelayCommand(ExecuteSearch);
        ClearSearchCommand = new RelayCommand(ExecuteClearSearch);
    }

    private void ExecuteSearch()
    {
        FilterNodes(SearchText);
    }

    private void ExecuteClearSearch()
    {
        SearchText = string.Empty;
        FilterNodes(string.Empty);
    }

    private void FilterNodes(string searchText)
    {
        FilteredChildren.Clear();

        if (RootNode == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(searchText))
        {
            foreach (var child in RootNode.Children)
            {
                FilteredChildren.Add(child);
            }

            return;
        }

        foreach (var node in SearchNodes(RootNode.Children, searchText.Trim()))
        {
            FilteredChildren.Add(node);
        }
    }

    private List<TreeNodeItem> SearchNodes(ObservableCollection<TreeNodeItem> nodes, string keyword)
    {
        var result = new List<TreeNodeItem>();

        foreach (var node in nodes)
        {
            var filteredNode = BuildFilteredNode(node, keyword);
            if (filteredNode != null)
            {
                result.Add(filteredNode);
            }
        }

        return result;
    }

    private TreeNodeItem? BuildFilteredNode(TreeNodeItem node, string keyword)
    {
        var matchesSearch = node.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                            node.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase);

        var filteredChildren = new List<TreeNodeItem>();
        foreach (var child in node.Children)
        {
            var filteredChild = BuildFilteredNode(child, keyword);
            if (filteredChild != null)
            {
                filteredChildren.Add(filteredChild);
            }
        }

        if (!matchesSearch && filteredChildren.Count == 0)
        {
            return null;
        }

        var clone = new TreeNodeItem(node.Name, node.NodeType, node.Icon)
        {
            DisplayName = node.DisplayName,
            Data = node.Data,
            IsExpanded = filteredChildren.Count > 0,
            IsLoading = node.IsLoading,
            IsSelected = node.IsSelected
        };

        foreach (var child in filteredChildren)
        {
            clone.AddChild(child);
        }

        return clone;
    }

    private void SetupEventHandlers()
    {
        if (DatabaseTreeView is TreeView treeView)
        {
            treeView.SelectionChanged += OnSelectionChanged;
            treeView.DoubleTapped += OnDoubleTapped;
        }
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DatabaseTreeView?.SelectedItem is TreeNodeItem selectedNode)
        {
            SelectedNode = selectedNode;
            NodeSelected?.Invoke(this, selectedNode);
        }
    }

    private void OnDoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (DatabaseTreeView?.SelectedItem is TreeNodeItem selectedNode)
        {
            NodeDoubleClicked?.Invoke(this, selectedNode);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == RootNodeProperty)
        {
            OnRootNodeChanged();
        }
        else if (change.Property == SearchTextProperty)
        {
            FilterNodes(SearchText);
        }
    }

    private void OnRootNodeChanged()
    {
        FilteredChildren.Clear();

        if (RootNode == null)
        {
            if (DatabaseTreeView != null)
            {
                DatabaseTreeView.ItemsSource = FilteredChildren;
            }

            return;
        }

        foreach (var child in RootNode.Children)
        {
            FilteredChildren.Add(child);
        }

        if (DatabaseTreeView != null)
        {
            DatabaseTreeView.ItemsSource = FilteredChildren;
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            FilterNodes(SearchText);
        }
    }
}
