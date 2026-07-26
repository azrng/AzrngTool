using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using AzrngTools.ViewModels;

namespace AzrngTools;

/// <summary>
/// 视图定位器
/// </summary>
[RequiresUnreferencedCode(
    "Default implementation of ViewLocator involves reflection which may be trimmed away.",
    Url = "https://docs.avaloniaui.net/docs/concepts/view-locator")]
public class ViewLocator : IDataTemplate
{
    private static readonly Dictionary<Type, Func<Control>> Registration = new();

    /// <summary>
    /// 同一 ViewModel 实例复用同一 View 实例，避免每次导航都重建控件树导致切换卡顿。
    /// ViewModel 被回收时对应 View 一并释放。
    /// </summary>
    private static readonly ConditionalWeakTable<object, Control> ViewCache = new();

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
            return ViewCache.GetValue(data, _ => factory());
        }

        return new TextBlock { Text = "Not Found: " + type };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase || (data is not null && Registration.ContainsKey(data.GetType()));
    }
}