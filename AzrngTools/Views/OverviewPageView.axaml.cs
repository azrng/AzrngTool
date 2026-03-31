using Avalonia.Controls;
using Avalonia.Controls.Notifications;

namespace AzrngTools.Views
{
    public partial class OverviewPageView : ViewControlBase
    {
        private WindowNotificationManager _manager;

        public OverviewPageView()
        {
            InitializeComponent();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            var topLevel = TopLevel.GetTopLevel(this);
            _manager = new WindowNotificationManager(topLevel) { MaxItems = 3 };
            App.NotificationPage = _manager;
        }
    }
}
