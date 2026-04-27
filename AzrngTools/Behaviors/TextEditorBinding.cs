using Avalonia;
using AvaloniaEdit;

namespace AzrngTools.Behaviors;

public sealed class TextEditorBinding
{
    public static readonly AttachedProperty<string?> TextProperty =
        AvaloniaProperty.RegisterAttached<TextEditorBinding, TextEditor, string?>(
            "Text",
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    private static readonly AttachedProperty<bool> IsSubscribedProperty =
        AvaloniaProperty.RegisterAttached<TextEditorBinding, TextEditor, bool>("IsSubscribed");

    private static readonly AttachedProperty<bool> IsUpdatingProperty =
        AvaloniaProperty.RegisterAttached<TextEditorBinding, TextEditor, bool>("IsUpdating");

    static TextEditorBinding()
    {
        TextProperty.Changed.AddClassHandler<TextEditor>(OnTextPropertyChanged);
    }

    public static string? GetText(AvaloniaObject element) => element.GetValue(TextProperty);

    public static void SetText(AvaloniaObject element, string? value) => element.SetCurrentValue(TextProperty, value);

    private static bool GetIsSubscribed(AvaloniaObject element) => element.GetValue(IsSubscribedProperty);

    private static void SetIsSubscribed(AvaloniaObject element, bool value) => element.SetValue(IsSubscribedProperty, value);

    private static bool GetIsUpdating(AvaloniaObject element) => element.GetValue(IsUpdatingProperty);

    private static void SetIsUpdating(AvaloniaObject element, bool value) => element.SetValue(IsUpdatingProperty, value);

    private static void OnTextPropertyChanged(TextEditor editor, AvaloniaPropertyChangedEventArgs args)
    {
        EnsureSubscribed(editor);

        if (GetIsUpdating(editor))
        {
            return;
        }

        var newText = args.NewValue as string ?? string.Empty;
        if (string.Equals(editor.Text, newText, StringComparison.Ordinal))
        {
            return;
        }

        var caretOffset = editor.CaretOffset;
        SetIsUpdating(editor, true);
        editor.Text = newText;
        editor.CaretOffset = Math.Min(caretOffset, editor.Text?.Length ?? 0);
        SetIsUpdating(editor, false);
    }

    private static void EnsureSubscribed(TextEditor editor)
    {
        if (GetIsSubscribed(editor))
        {
            return;
        }

        editor.TextChanged += OnEditorTextChanged;
        SetIsSubscribed(editor, true);
    }

    private static void OnEditorTextChanged(object? sender, EventArgs args)
    {
        if (sender is not TextEditor editor || GetIsUpdating(editor))
        {
            return;
        }

        SetIsUpdating(editor, true);
        SetText(editor, editor.Text);
        SetIsUpdating(editor, false);
    }
}
