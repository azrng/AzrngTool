using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.Media;
using Avalonia.Media.Transformation;

namespace AzrngTools.Controls.Database;

public partial class LoadingOverlay : UserControl
{
    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<LoadingOverlay, bool>(nameof(IsLoading));

    public static readonly StyledProperty<string> LoadingTextProperty =
        AvaloniaProperty.Register<LoadingOverlay, string>(nameof(LoadingText), "加载中...");

    private Grid? _rootGrid;
    private TextBlock? _partLoadingText;
    private Border? _spinnerBorder;
    private double _currentAngle;
    private DispatcherTimer? _animationTimer;

    public bool IsLoading
    {
        get => GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public string LoadingText
    {
        get => GetValue(LoadingTextProperty);
        set => SetValue(LoadingTextProperty, value);
    }

    public LoadingOverlay()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _rootGrid = this.FindControl<Grid>("RootGrid");
        _partLoadingText = this.FindControl<TextBlock>("PART_LoadingText");
        _spinnerBorder = this.FindControl<Border>("SpinnerBorder");
        UpdateVisibility();
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _animationTimer?.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsLoadingProperty)
        {
            UpdateVisibility();
        }
        else if (change.Property == LoadingTextProperty && _partLoadingText != null)
        {
            _partLoadingText.Text = LoadingText;
        }
    }

    private void UpdateVisibility()
    {
        if (_rootGrid != null)
        {
            _rootGrid.IsVisible = IsLoading;

            if (IsLoading)
            {
                StartAnimation();
            }
            else
            {
                StopAnimation();
            }
        }
    }

    private void StartAnimation()
    {
        if (_animationTimer != null)
            return;

        _animationTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(16),
            DispatcherPriority.Background,
            (s, e) =>
            {
                _currentAngle = (_currentAngle + 10) % 360;
                if (_spinnerBorder != null)
                {
                    _spinnerBorder.RenderTransform = new RotateTransform(_currentAngle);
                    _spinnerBorder.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
                }
            });
        _animationTimer.Start();
    }

    private void StopAnimation()
    {
        _animationTimer?.Stop();
        _animationTimer = null;
        _currentAngle = 0;
        if (_spinnerBorder != null)
        {
            _spinnerBorder.RenderTransform = null;
        }
    }
}
