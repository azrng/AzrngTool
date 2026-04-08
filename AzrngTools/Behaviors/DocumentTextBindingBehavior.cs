using Avalonia.Xaml.Interactivity;
using AvaloniaEdit;
using System;

namespace AzrngTools.Behaviors
{
    public class DocumentTextBindingBehavior : Behavior<TextEditor>
    {
        private TextEditor? _textEditor;
        private bool _isUpdatingText = false;

        public static readonly StyledProperty<string?> TextProperty =
            AvaloniaProperty.Register<DocumentTextBindingBehavior, string?>(nameof(Text));

        public string? Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();

            if (AssociatedObject is TextEditor textEditor)
            {
                _textEditor = textEditor;
                _textEditor.TextChanged += TextChanged;

                // 监听Text属性变化
                this.PropertyChanged += OnTextPropertyChanged;
            }
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();

            if (_textEditor != null)
            {
                _textEditor.TextChanged -= TextChanged;
            }

            this.PropertyChanged -= OnTextPropertyChanged;
        }

        private void TextChanged(object? sender, EventArgs eventArgs)
        {
            if (_textEditor != null && _textEditor.Document != null && !_isUpdatingText)
            {
                _isUpdatingText = true;
                Text = _textEditor.Document.Text;
                _isUpdatingText = false;
            }
        }

        private void OnTextPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == TextProperty && !_isUpdatingText)
            {
                var newText = e.NewValue as string;
                if (_textEditor != null && _textEditor.Document != null && newText != null)
                {
                    _isUpdatingText = true;
                    var caretOffset = _textEditor.CaretOffset;
                    _textEditor.Document.Text = newText;
                    _textEditor.CaretOffset = Math.Min(caretOffset, newText.Length);
                    _isUpdatingText = false;
                }
            }
        }
    }
}
