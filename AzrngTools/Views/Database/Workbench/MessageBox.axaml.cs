using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.Input;

namespace AzrngTools.Views.Database.Workbench;

/// <summary>
/// 消息框按钮类型
/// </summary>
public enum MessageBoxButtons
{
    OK,
    OKCancel,
    YesNo,
    YesNoCancel
}

/// <summary>
/// 消息框按钮结果
/// </summary>
public enum MessageBoxButtonType
{
    None,
    OK,
    Cancel,
    Yes,
    No
}

/// <summary>
/// 消息框窗口
/// </summary>
public partial class MessageBox : Window
{
    /// <summary>
    /// 消息内容
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// 按钮类型
    /// </summary>
    public MessageBoxButtons Buttons { get; set; } = MessageBoxButtons.OK;

    /// <summary>
    /// 默认按钮
    /// </summary>
    public MessageBoxButtonType DefaultButton { get; set; } = MessageBoxButtonType.None;

    /// <summary>
    /// 任务完成源
    /// </summary>
    private TaskCompletionSource<MessageBoxButtonType> _taskCompletionSource = new();

    public MessageBox()
    {
        InitializeComponent();
        InitializeCommands();
        UpdateButtons();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        DataContext = this;
    }

    private void InitializeCommands()
    {
        OkCommand = new RelayCommand(OnOk);
        CancelCommand = new RelayCommand(OnCancel);
        YesCommand = new RelayCommand(OnYes);
        NoCommand = new RelayCommand(OnNo);
    }

    /// <summary>
    /// 更新按钮显示
    /// </summary>
    private void UpdateButtons()
    {
        ShowOkButton = Buttons == MessageBoxButtons.OK || Buttons == MessageBoxButtons.OKCancel;
        ShowCancelButton = Buttons == MessageBoxButtons.OKCancel || Buttons == MessageBoxButtons.YesNoCancel;
        ShowYesButton = Buttons == MessageBoxButtons.YesNo || Buttons == MessageBoxButtons.YesNoCancel;
        ShowNoButton = Buttons == MessageBoxButtons.YesNo || Buttons == MessageBoxButtons.YesNoCancel;

        // 设置默认按钮
        if (DefaultButton != MessageBoxButtonType.None)
        {
            // TODO: 设置默认按钮焦点
        }
    }

    /// <summary>
    /// 是否显示 OK 按钮
    /// </summary>
    public bool ShowOkButton { get; set; }

    /// <summary>
    /// 是否显示 Cancel 按钮
    /// </summary>
    public bool ShowCancelButton { get; set; }

    /// <summary>
    /// 是否显示 Yes 按钮
    /// </summary>
    public bool ShowYesButton { get; set; }

    /// <summary>
    /// 是否显示 No 按钮
    /// </summary>
    public bool ShowNoButton { get; set; }

    /// <summary>
    /// OK 命令
    /// </summary>
    public RelayCommand OkCommand { get; private set; } = null!;

    /// <summary>
    /// Cancel 命令
    /// </summary>
    public RelayCommand CancelCommand { get; private set; } = null!;

    /// <summary>
    /// Yes 命令
    /// </summary>
    public RelayCommand YesCommand { get; private set; } = null!;

    /// <summary>
    /// No 命令
    /// </summary>
    public RelayCommand NoCommand { get; private set; } = null!;

    private void OnOk()
    {
        _taskCompletionSource.SetResult(MessageBoxButtonType.OK);
        Close();
    }

    private void OnCancel()
    {
        _taskCompletionSource.SetResult(MessageBoxButtonType.Cancel);
        Close();
    }

    private void OnYes()
    {
        _taskCompletionSource.SetResult(MessageBoxButtonType.Yes);
        Close();
    }

    private void OnNo()
    {
        _taskCompletionSource.SetResult(MessageBoxButtonType.No);
        Close();
    }

    /// <summary>
    /// 显示对话框并获取结果
    /// </summary>
    public Task<MessageBoxButtonType> ShowDialogAsync(Window parent)
    {
        _taskCompletionSource = new TaskCompletionSource<MessageBoxButtonType>();
        ShowDialog(parent);
        return _taskCompletionSource.Task;
    }

    /// <summary>
    /// 窗口关闭时处理
    /// </summary>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!_taskCompletionSource.Task.IsCompleted)
        {
            _taskCompletionSource.SetResult(DefaultButton != MessageBoxButtonType.None ? DefaultButton : MessageBoxButtonType.Cancel);
        }
        base.OnClosing(e);
    }
}
