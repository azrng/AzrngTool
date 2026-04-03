using System;
using Avalonia.Controls;
using AzrngTools.ViewModels.Database;

namespace AzrngTools.Views.Database.Workbench;

public partial class CommentEditorDialog : Window
{
    public CommentEditorDialogViewModel ViewModel => (CommentEditorDialogViewModel)DataContext!;

    public CommentEditorDialog()
        : this(new CommentEditorDialogViewModel())
    {
    }

    public CommentEditorDialog(CommentEditorDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        ViewModel.CloseRequested += OnCloseRequested;
        Closed += OnDialogClosed;
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        Close(ViewModel.DialogResult);
    }

    private void OnDialogClosed(object? sender, EventArgs e)
    {
        ViewModel.CloseRequested -= OnCloseRequested;
        Closed -= OnDialogClosed;
    }
}
