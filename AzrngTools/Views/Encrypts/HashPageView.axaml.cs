using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using AzrngTools.Utils.Events;
using CommunityToolkit.Mvvm.Messaging;

namespace AzrngTools.Views.Encrypts
{
    public partial class HashPageView : ViewControlBase
    {
        public HashPageView()
        {
            InitializeComponent();

            var topLevel = TopLevel.GetTopLevel(this);
            // 右上角通知下移避开无边框窗口的标题栏与圆角，原因同 MainWindow 的 Toast 偏移
            var manager = new WindowNotificationManager(topLevel)
            {
                MaxItems = 3,
                Position = NotificationPosition.TopRight,
                Margin = new Avalonia.Thickness(0, 40, 8, 0),
            };

            // 处理消息
            WeakReferenceMessenger.Default.Register<MessageModel, string>(this, "Main", (r, m) =>
            {
                manager?.Show(new Notification(m.Title, m.Message));
            });
        }
    }
}