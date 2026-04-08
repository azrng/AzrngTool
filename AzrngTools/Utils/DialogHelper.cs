using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace AzrngTools.Utils
{
    public class DialogHelper
    {
        /// <summary>
        /// 显示已注册上下文(很可能是ViewModel)的打开文件对话框
        /// </summary>
        /// <param name="context">The context</param>
        /// <param name="title">The dialog title or a default is null</param>
        /// <param name="selectMany">Is selecting many files allowed?</param>
        /// <returns>An array of file names</returns>
        /// <exception cref="ArgumentNullException">if context was null</exception>
        public static async Task<List<string>> OpenFileDialogAsync(object context, string? title = null,
                                                                   bool selectMany = true)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            // lookup the TopLevel for the context
            var topLevel = DialogManager.GetTopLevelForContext(context);
            if (topLevel != null)
            {
                // Open the file dialog
                var storageFiles = await topLevel.StorageProvider.OpenFilePickerAsync(
                    new FilePickerOpenOptions { AllowMultiple = selectMany, Title = title ?? "请选择文件" });

                // return the result
                return storageFiles.Select(s => s.Path.LocalPath).ToList();
            }

            return [];
        }

        /// <summary>
        /// 保存文件
        /// </summary>
        /// <param name="context">The context</param>
        /// <param name="title">The dialog title or a default is null</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static async Task<bool> SaveFileDialogAsync(object context, string? title = null)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            // lookup the TopLevel for the context
            var topLevel = DialogManager.GetTopLevelForContext(context);

            if (topLevel == null)
            {
                return false;
            }

            var result = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = title ?? "请选择保存路径" });

            return result is not null;
        }
    }

    public class DialogManager
    {
        private static readonly Dictionary<object, Visual> RegistrationMapper =
            new Dictionary<object, Visual>();

        static DialogManager()
        {
            RegisterProperty.Changed.AddClassHandler<Visual>(RegisterChanged);
        }

        private static void RegisterChanged(Visual sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (sender is null)
            {
                throw new InvalidOperationException("The DialogManager can only be registered on a Visual");
            }

            // Unregister any old registered context
            if (e.OldValue != null)
            {
                RegistrationMapper.Remove(e.OldValue);
            }

            // Register any new context
            if (e.NewValue != null)
            {
                RegistrationMapper.Add(e.NewValue, sender);
            }
        }

        /// <summary>
        /// This property handles the registration of Views and ViewModel
        /// </summary>
        public static readonly AttachedProperty<object?> RegisterProperty =
            AvaloniaProperty.RegisterAttached<DialogManager, Visual, object?>("Register");

        /// <summary>
        /// Accessor for Attached property <see cref="RegisterProperty"/>.
        /// </summary>
        public static void SetRegister(AvaloniaObject element, object? value)
        {
            element.SetValue(RegisterProperty, value);
        }

        /// <summary>
        /// Accessor for Attached property <see cref="RegisterProperty"/>.
        /// </summary>
        public static object? GetRegister(AvaloniaObject element)
        {
            return element.GetValue(RegisterProperty);
        }

        /// <summary>
        /// Gets the associated <see cref="Visual"/> for a given context. Returns null, if none was registered
        /// </summary>
        /// <param name="context">The context to lookup</param>
        /// <returns>The registered Visual for the context or null if none was found</returns>
        public static Visual? GetVisualForContext(object context)
        {
            return RegistrationMapper.GetValueOrDefault(context);
        }

        /// <summary>
        /// Gets the parent <see cref="TopLevel"/> for the given context. Returns null, if no TopLevel was found
        /// </summary>
        /// <param name="context">The context to lookup</param>
        /// <returns>The registered TopLevel for the context or null if none was found</returns>
        public static TopLevel? GetTopLevelForContext(object context)
        {
            return TopLevel.GetTopLevel(GetVisualForContext(context));
        }
    }
}
