using Avalonia.Controls;
using Avalonia.Controls.Notifications;

namespace AzrngTools.Views
{
    public partial class OverviewPageView : ViewControlBase
    {
        private WindowNotificationManager? _manager;

        public OverviewPageView()
        {
            InitializeComponent();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            // 视图被 ViewLocator 缓存复用会反复挂载，通知管理器只允许创建一次，
            // 否则每次回到首页都会向窗口叠加一个新实例（占位符 + 事件订阅同步累积）
            if (_manager is not null)
            {
                return;
            }

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null)
            {
                return;
            }

            _manager = new WindowNotificationManager(topLevel) { MaxItems = 3 };
            App.NotificationPage = _manager;
        }
    }
}
