using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AzrngTools.Models.Database.DTOs;

namespace AzrngTools.ViewModels.Database;

public partial class CommentEditorDialogViewModel : ViewModelBase
{
    private readonly Func<string, Task<(bool Success, string? ErrorMessage)>>? _saveAsync;

    [ObservableProperty]
    private string _dialogTitle = "编辑备注";

    [ObservableProperty]
    private string _primaryLabel = string.Empty;

    [ObservableProperty]
    private string _primaryValue = string.Empty;

    [ObservableProperty]
    private bool _showSecondaryInfo;

    [ObservableProperty]
    private string _secondaryLabel = string.Empty;

    [ObservableProperty]
    private string _secondaryValue = string.Empty;

    [ObservableProperty]
    private string _originalComment = string.Empty;

    [ObservableProperty]
    private string _editableComment = string.Empty;

    [ObservableProperty]
    private string _hintText = string.Empty;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public event EventHandler? CloseRequested;

    public CommentEditorDialogResultDto? DialogResult { get; private set; }

    public string OriginalCommentDisplay => string.IsNullOrWhiteSpace(OriginalComment)
        ? "暂无备注"
        : OriginalComment;

    public bool HasChanges => !string.Equals(
        NormalizeComment(OriginalComment),
        NormalizeComment(EditableComment),
        StringComparison.Ordinal);

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public CommentEditorDialogViewModel()
    {
    }

    public CommentEditorDialogViewModel(Func<string, Task<(bool Success, string? ErrorMessage)>> saveAsync)
    {
        _saveAsync = saveAsync;
    }

    partial void OnEditableCommentChanged(string value)
    {
        OnPropertyChanged(nameof(HasChanges));
        SaveCommand.NotifyCanExecuteChanged();
    }

    partial void OnOriginalCommentChanged(string value)
    {
        OnPropertyChanged(nameof(OriginalCommentDisplay));
        OnPropertyChanged(nameof(HasChanges));
        SaveCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsSavingChanged(bool value)
    {
        SaveCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        ErrorMessage = string.Empty;

        if (_saveAsync != null)
        {
            IsSaving = true;
            try
            {
                var (success, errorMessage) = await _saveAsync(EditableComment ?? string.Empty);
                if (!success)
                {
                    ErrorMessage = string.IsNullOrWhiteSpace(errorMessage)
                        ? "保存失败，请稍后重试。"
                        : errorMessage!;
                    return;
                }
            }
            finally
            {
                IsSaving = false;
            }
        }

        DialogResult = new CommentEditorDialogResultDto
        {
            Confirmed = true,
            Comment = EditableComment ?? string.Empty
        };

        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        DialogResult = new CommentEditorDialogResultDto
        {
            Confirmed = false,
            Comment = string.Empty
        };

        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private bool CanSave() => !IsSaving && HasChanges;

    private bool CanCancel() => !IsSaving;

    private static string NormalizeComment(string? comment) => comment ?? string.Empty;
}
