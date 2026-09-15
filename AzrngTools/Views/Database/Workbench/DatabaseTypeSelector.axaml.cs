using Avalonia.Controls;

namespace AzrngTools.Views.Database.Workbench;

// 类型卡片点击直接通过 XAML 的 SelectDatabaseTypeCommand 绑定转发，
// 不再需要 code-behind 的事件注册
public partial class DatabaseTypeSelector : UserControl
{
    public DatabaseTypeSelector()
    {
        InitializeComponent();
    }
}
