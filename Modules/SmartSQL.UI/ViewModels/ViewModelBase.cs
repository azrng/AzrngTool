using CommunityToolkit.Mvvm.ComponentModel;

namespace SmartSQL.UI.ViewModels;

/// <summary>
/// ViewModel 基类
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    /// <summary>
    /// 设置属性值
    /// </summary>
    /// <typeparam name="T">属性类型</typeparam>
    /// <param name="field">字段引用</param>
    /// <param name="value">新值</param>
    /// <param name="propertyName">属性名称</param>
    /// <returns>是否发生了更改</returns>
    protected new bool SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// 设置属性值并执行回调
    /// </summary>
    /// <typeparam name="T">属性类型</typeparam>
    /// <param name="field">字段引用</param>
    /// <param name="value">新值</param>
    /// <param name="callback">值更改后的回调</param>
    /// <param name="propertyName">属性名称</param>
    /// <returns>是否发生了更改</returns>
    protected bool SetProperty<T>(ref T field, T value, System.Action callback, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        callback?.Invoke();
        return true;
    }

    /// <summary>
    /// 是否正在加载
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// 加载状态文本
    /// </summary>
    [ObservableProperty]
    private string? _loadingText;

    /// <summary>
    /// 错误消息
    /// </summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// 设置加载状态
    /// </summary>
    /// <param name="isLoading">是否正在加载</param>
    /// <param name="loadingText">加载文本</param>
    protected void SetLoading(bool isLoading, string? loadingText = null)
    {
        IsLoading = isLoading;
        LoadingText = loadingText;
        if (!isLoading)
        {
            LoadingText = null;
        }
    }

    /// <summary>
    /// 设置错误消息
    /// </summary>
    /// <param name="errorMessage">错误消息</param>
    protected void SetError(string? errorMessage)
    {
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// 清除错误消息
    /// </summary>
    protected void ClearError()
    {
        ErrorMessage = null;
    }
}
