using System.Text.Json;
using AzrngTools.Services;
using Xunit;

namespace AzrngTools.Tests.Services;

// 页面输入存档回归：防抖合并落盘、原子写、损坏兜底、超大值跳过与版本兼容（DES-SETTINGS-002 验证方式）
public class PageInputStoreServiceTests
{
    private const string PageId = "format-json";

    [Fact]
    public void SaveThenFlush_WritesArchiveFileWithoutTempResidue()
    {
        var directory = CreateTempDirectory();
        try
        {
            var service = new PageInputStoreService(directory);
            service.Save(PageId, new Dictionary<string, string?>
            {
                ["Original"] = "{ \"a\": 1 }",
            });
            service.FlushPendingPagesForTests();

            var filePath = Path.Combine(directory, PageId + ".json");
            Assert.True(File.Exists(filePath), "防抖冲刷后存档文件应已落盘。");
            Assert.False(File.Exists(filePath + ".tmp"), "原子替换后不应残留临时文件。");

            var json = File.ReadAllText(filePath);
            var document = JsonDocument.Parse(json);
            Assert.Equal(1, document.RootElement.GetProperty("version").GetInt32());
            Assert.Equal("{ \"a\": 1 }", document.RootElement.GetProperty("inputs").GetProperty("Original").GetString());
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Save_TwiceWithinDebounceWindow_OnlyLatestSnapshotPersisted()
    {
        var directory = CreateTempDirectory();
        try
        {
            var service = new PageInputStoreService(directory);
            service.Save(PageId, new Dictionary<string, string?> { ["Original"] = "第一次" });
            service.Save(PageId, new Dictionary<string, string?> { ["Original"] = "第二次" });
            service.FlushPendingPagesForTests();

            var reloaded = new PageInputStoreService(directory);
            var restored = reloaded.TryLoad(PageId);
            Assert.Single(restored);
            Assert.Equal("第二次", restored["Original"]);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void TryLoad_WithoutArchiveFile_ReturnsEmpty()
    {
        var directory = CreateTempDirectory();
        try
        {
            var service = new PageInputStoreService(directory);

            Assert.Empty(service.TryLoad(PageId));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void TryLoad_WithCorruptedFile_ReturnsEmptyWithoutThrow()
    {
        var directory = CreateTempDirectory();
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, PageId + ".json"), "{ 不是 JSON");

            var service = new PageInputStoreService(directory);

            Assert.Empty(service.TryLoad(PageId));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void TryLoad_WithUnknownVersion_TreatsAsCorrupted()
    {
        var directory = CreateTempDirectory();
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory, PageId + ".json"),
                "{\"version\":999,\"inputs\":{\"Original\":\"x\"}}");

            var service = new PageInputStoreService(directory);

            Assert.Empty(service.TryLoad(PageId));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Save_WithOversizedValue_SkipsThatKeyKeepsOthers()
    {
        var directory = CreateTempDirectory();
        try
        {
            var service = new PageInputStoreService(directory);
            service.Save(PageId, new Dictionary<string, string?>
            {
                ["Original"] = new string('a', 2_000_001),
                ["Option"] = "true",
            });
            service.FlushPendingPagesForTests();

            var reloaded = new PageInputStoreService(directory);
            var restored = reloaded.TryLoad(PageId);
            Assert.False(restored.ContainsKey("Original"), "超过上限的值应整键跳过。");
            Assert.Equal("true", restored["Option"]);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Save_WithInvalidPageId_DoesNotWrite()
    {
        var directory = CreateTempDirectory();
        try
        {
            var service = new PageInputStoreService(directory);
            service.Save("../escape", new Dictionary<string, string?> { ["Original"] = "x" });
            service.FlushPendingPagesForTests();

            Assert.Empty(Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void SaveFlushThenSavePending_PendingSurvivesAcrossFlush()
    {
        var directory = CreateTempDirectory();
        try
        {
            var service = new PageInputStoreService(directory);
            service.Save(PageId, new Dictionary<string, string?> { ["Original"] = "第一页" });
            service.FlushPendingPagesForTests();

            // 冲刷后再次提交：验证待写集合回收正常，新快照仍能落盘
            service.Save(PageId, new Dictionary<string, string?> { ["Original"] = "第二页" });
            service.FlushPendingPagesForTests();

            var reloaded = new PageInputStoreService(directory);
            var restored = reloaded.TryLoad(PageId);
            Assert.Single(restored);
            Assert.Equal("第二页", restored["Original"]);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Save_WithEmptyInputs_DoesNotOverwriteExistingArchive()
    {
        var directory = CreateTempDirectory();
        try
        {
            var service = new PageInputStoreService(directory);
            service.Save(PageId, new Dictionary<string, string?> { ["Original"] = "有内容" });
            service.FlushPendingPagesForTests();

            service.Save(PageId, new Dictionary<string, string?>());
            service.FlushPendingPagesForTests();

            var reloaded = new PageInputStoreService(directory);
            var restored = reloaded.TryLoad(PageId);
            Assert.Single(restored);
            Assert.Equal("有内容", restored["Original"]);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AzrngTools.Tests", "page-input-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
