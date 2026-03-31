using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Interactivity;
using AzrngTools.Utils.Events;
using AzrngTools.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace AzrngTools.Views
{
    public partial class MainWindow : Window, IScopedDependency
    {
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = App.Current.Services.GetRequiredService<MainWindowViewModel>();

            // 处理消息  在首页保存通知页面信息
            WeakReferenceMessenger.Default.Register<MessageModel, string>(this, "Main", (r, m) =>
            {
                App.NotificationPage?.Show(new Notification(m.Title, m.Message));
            });
        }

        /// <summary>
        /// 实现拖动效果
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HeaderBorder_OnPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (e.Pointer.Type == PointerType.Mouse) this.BeginMoveDrag(e);
        }

        /// <summary>
        /// 最小化
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnMin_OnClick(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        /// <summary>
        /// 最大化
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnMax_OnClick(object sender, RoutedEventArgs e)
        {
            this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        /// <summary>
        /// 退出
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnClose_OnClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// 窗口双击放大缩小事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HeaderBorder_OnDoubleTapped(object sender, TappedEventArgs e)
        {
            this.WindowState = this.WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
        }
    }
}
