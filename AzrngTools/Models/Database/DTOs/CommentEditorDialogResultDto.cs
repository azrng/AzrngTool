namespace AzrngTools.Models.Database.DTOs;

public sealed class CommentEditorDialogResultDto
{
    public bool Confirmed { get; init; }

    public string Comment { get; init; } = string.Empty;
}
