using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace AzrngTools.Controls.Database;

public enum MessageType
{
    Success,
    Error,
    Warning,
    Info
}

public partial class ToastNotification : UserControl
{
    public static readonly new StyledProperty<bool> IsVisibleProperty =
        AvaloniaProperty.Register<ToastNotification, bool>(nameof(IsVisible));

    public static readonly StyledProperty<MessageType> MessageTypeProperty =
        AvaloniaProperty.Register<ToastNotification, MessageType>(nameof(MessageType));

    public static readonly StyledProperty<string> MessageProperty =
        AvaloniaProperty.Register<ToastNotification, string>(nameof(Message), string.Empty);

    public static readonly StyledProperty<int> AutoCloseDelayProperty =
        AvaloniaProperty.Register<ToastNotification, int>(nameof(AutoCloseDelay), 3000);

    private Border? _rootBorder;
    private TextBlock? _iconText;
    private TextBlock? _messageText;
    private Button? _closeButton;
    private DispatcherTimer? _closeTimer;

    public new bool IsVisible
    {
        get => GetValue(IsVisibleProperty);
        set => SetValue(IsVisibleProperty, value);
    }

    public MessageType MessageType
    {
        get => GetValue(MessageTypeProperty);
        set => SetValue(MessageTypeProperty, value);
    }

    public string Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public int AutoCloseDelay
    {
        get => GetValue(AutoCloseDelayProperty);
        set => SetValue(AutoCloseDelayProperty, value);
    }

    public event EventHandler? Closed;

    public ToastNotification()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _rootBorder = this.FindControl<Border>("RootBorder");
        _iconText = this.FindControl<TextBlock>("IconText");
        _messageText = this.FindControl<TextBlock>("MessageText");
        _closeButton = this.FindControl<Button>("CloseButton");

        if (_closeButton != null)
        {
            _closeButton.Click += CloseButton_Click;
        }

        UpdateAppearance();
        StartAutoCloseTimer();
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _closeTimer?.Stop();
        _closeTimer = null;

        if (_closeButton != null)
        {
            _closeButton.Click -= CloseButton_Click;
        }

        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsVisibleProperty)
        {
            UpdateVisibility();
        }
        else if (change.Property == MessageTypeProperty || change.Property == MessageProperty)
        {
            UpdateAppearance();
        }
    }

    private void UpdateVisibility()
    {
        if (_rootBorder != null)
        {
            _rootBorder.IsVisible = IsVisible;
        }
    }

    private void UpdateAppearance()
    {
        if (_rootBorder == null || _iconText == null || _messageText == null)
        {
            return;
        }

        _rootBorder.Classes.Clear();

        switch (MessageType)
        {
            case MessageType.Success:
                _rootBorder.Classes.Add("success");
                _iconText.Text = "OK";
                break;
            case MessageType.Error:
                _rootBorder.Classes.Add("error");
                _iconText.Text = "X";
                break;
            case MessageType.Warning:
                _rootBorder.Classes.Add("warning");
                _iconText.Text = "!";
                break;
            case MessageType.Info:
                _rootBorder.Classes.Add("info");
                _iconText.Text = "i";
                break;
        }

        _messageText.Text = Message;
    }

    private void StartAutoCloseTimer()
    {
        if (AutoCloseDelay <= 0)
        {
            return;
        }

        _closeTimer?.Stop();
        _closeTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(AutoCloseDelay),
            DispatcherPriority.Background,
            (_, _) =>
            {
                Hide();
                _closeTimer?.Stop();
            });

        _closeTimer.Start();
    }

    public void Show(string message, MessageType type = MessageType.Info, int autoCloseDelay = 3000)
    {
        Message = message;
        MessageType = type;
        AutoCloseDelay = autoCloseDelay;
        IsVisible = true;
        StartAutoCloseTimer();
    }

    public void Hide()
    {
        IsVisible = false;
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        _closeTimer?.Stop();
        Hide();
    }
}
