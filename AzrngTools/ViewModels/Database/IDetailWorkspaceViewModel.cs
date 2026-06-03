using AzrngTools.Models.Database;

namespace AzrngTools.ViewModels.Database;

public interface IDetailWorkspaceViewModel
{
    ConnectionConfig? CurrentConnection { get; set; }
    bool ShowObjectList { get; set; }
}
