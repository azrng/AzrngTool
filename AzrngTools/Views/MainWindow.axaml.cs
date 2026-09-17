using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using AzrngTools.Models;
using AzrngTools.Services;
using AzrngTools.Utils.Events;
using AzrngTools.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Ursa.Controls;

namespace AzrngTools.Views
{
    public partial class MainWindow : UrsaWindow, IScopedDependency
    {
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = App.Current.Services.GetRequiredService<MainWindowViewModel>();
            Opened += OnWindowOpened;
            Closed += OnWindowClosed;

            // 处理消息  在首页保存通知页面信息
            WeakReferenceMessenger.Default.Register<MessageModel, string>(this, "Main", (r, m) =>
            {
                App.NotificationPage?.Show(new Avalonia.Controls.Notifications.Notification(m.Title, m.Message));
            });
        }

        private void OnWindowOpened(object? sender, System.EventArgs e)
        {
            // UrsaWindow 为无边框窗口，客户区从窗口物理顶端开始；Semi 主题把 Toast 定位在
            // 客户区顶部居中且 Margin=0，直接弹出会被窗口顶边和圆角裁掉，整体下移到标题栏
            // （52px，见 Styles/WindowDecorations.axaml 的 DefaultTitleBarHeight）以下显示
            var manager = new WindowToastManager(this)
            {
                MaxItems = 3,
                Margin = new Avalonia.Thickness(0, 60, 0, 8),
            };
            ToastService.SetManager(manager);
        }

        private void OnWindowClosed(object? sender, System.EventArgs e)
        {
            Opened -= OnWindowOpened;
            Closed -= OnWindowClosed;
            ToastService.ClearManager();
        }

        private void OnCommonListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox { SelectedItem: MenuBar menu } && DataContext is MainWindowViewModel vm)
            {
                if (!ReferenceEquals(vm.SelectedListItem, menu))
                    vm.SelectMenuCommand.Execute(menu);
            }
        }

        private ListBox? _lastNavListBox;

        private void OnGroupListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (sender is not ListBox listBox || DataContext is not MainWindowViewModel vm)
            {
                return;
            }

            // 各分组使用独立的 ListBox 维护选中状态；切换到其他分组时需清空上一组的选中，
            // 否则回到原分组再次点击已选中项时 ListBox 不触发 SelectionChanged，导致页面切不回去。
            // 清空时 SelectedItem 变为 null，反向触发本方法不会匹配到 MenuBar，因此安全。
            if (!ReferenceEquals(_lastNavListBox, listBox))
            {
                if (_lastNavListBox is { } previous)
                {
                    previous.SelectedIndex = -1;
                }

                _lastNavListBox = listBox;
            }

            if (listBox.SelectedItem is MenuBar menu && !ReferenceEquals(vm.SelectedListItem, menu))
            {
                vm.SelectMenuCommand.Execute(menu);
            }
        }
    }
}
