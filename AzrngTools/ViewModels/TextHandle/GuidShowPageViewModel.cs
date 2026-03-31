using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AzrngTools.ViewModels.TextHandle;

/// <summary>
/// guid显示
/// </summary>
public partial class GuidShowPageViewModel : ViewModelBase
{
    public GuidShowPageViewModel()
    {
        GenerateGuid();
    }

    #region 属性

    [ObservableProperty]
    private string _guidN;

    [ObservableProperty]
    private string _guidD;

    [ObservableProperty]
    private string _guidB;

    [ObservableProperty]
    private string _guidP;

    [ObservableProperty]
    private string _guidX;

    #endregion

    /// <summary>
    /// 生成guid
    /// </summary>
    [RelayCommand]
    private void GenerateGuid()
    {
        var guid = Guid.NewGuid();
        GuidN = guid.ToString("N");
        GuidD = guid.ToString("D");
        GuidB = guid.ToString("B");
        GuidP = guid.ToString("P");
        GuidX = guid.ToString("X");
    }
}