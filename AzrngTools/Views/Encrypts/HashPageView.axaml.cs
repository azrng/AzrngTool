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
            var manager = new WindowNotificationManager(topLevel) { MaxItems = 3 };

            // 处理消息
            WeakReferenceMessenger.Default.Register<MessageModel, string>(this, "Main", (r, m) =>
            {
                manager?.Show(new Notification(m.Title, m.Message));
            });
        }
    }
}