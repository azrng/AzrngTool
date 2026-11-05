using AzrngTools.Services;

namespace AzrngTools.Tests.TestDoubles;

/// <summary>
/// 测试用页面输入存档服务工厂：每个用例独立临时目录，避免用例间存档串扰；
/// 不关心存档内容的用例用完即弃，目录留在系统临时区由系统清理。
/// </summary>
internal static class TestPageInputStores
{
    public static PageInputStoreService Create()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "AzrngTools.Tests",
            "page-input-vm-" + Guid.NewGuid().ToString("N"));
        return new PageInputStoreService(directory);
    }
}
