using Avalonia.Controls;
using Avalonia.Controls.Templates;
using AzrngTools.ViewModels;

namespace AzrngTools;

// /// <summary>
// /// 视图定位器
// /// </summary>
// public class ViewLocator : IDataTemplate
// {
//     public Control Build(object data)
//     {
//         if (data is null)
//             return null;
//
//         var name = data.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
//         var type = Type.GetType(name);
//
//         if (type != null)
//         {
//             var control = (Control)Activator.CreateInstance(type)!;
//             control.DataContext = data;
//             return control;
//         }
//
//         return new TextBlock { Text = "Not Found: " + name };
//     }
//
//     public bool Match(object data)
//     {
//         return data is ViewModelBase;
//     }
// }

/// <summary>
/// 视图定位器
/// </summary>
public class ViewLocator : IDataTemplate
{
    private static readonly Dictionary<Type, Func<Control>> Registration = new();

    public static void Register<TViewModel, TView>() where TView : Control, new()
    {
        Registration.Add(typeof(TViewModel), () => new TView());
    }

    public static void Register<TViewModel, TView>(Func<TView> factory) where TView : Control, new()
    {
        Registration.Add(typeof(TViewModel), factory);
    }

    public Control? Build(object? data)
    {
        if (data is null)
        {
            return null;
        }

        var type = data.GetType();

        if (Registration.TryGetValue(type, out var factory))
        {
            return factory();
        }

        return new TextBlock { Text = "Not Found: " + type };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase || (data is not null && Registration.ContainsKey(data.GetType()));
    }
}
