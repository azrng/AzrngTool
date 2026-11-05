using System.Security.Cryptography;
using System.Text.Json;
using AzrngTools.Services;
using AzrngTools.Tests.TestDoubles;
using AzrngTools.ViewModels.Encrypts;
using Common.Security.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AzrngTools.Tests.ViewModels.Encrypts;

// AES 页输入记忆回归：页面构造恢复、恢复赋值不回写、敏感键（Secret/Iv）绝不落盘（DES-SETTINGS-002）
public class AesEncryptInputRestoreTests
{
    private const string PageId = "encrypts-aes";

    [Fact]
    public void SetOriginalAndOptionsThenNewInstance_RestoresInputs()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = new PageInputStoreService(directory);
            var first = new AesEncryptPageViewModel(
                new TestMessageService(), NullLogger<DesEncryptPageViewModel>.Instance, store);
            first.Original = "待加密的原文";
            first.OutTypeIndex = (int)OutType.Hex;
            first.CipherModeIndex = (int)CipherMode.CBC;
            store.FlushPendingPagesForTests();

            // 页面实例经 _pageCache 缓存复用，这里用新实例模拟应用重启后首次进入页面
            var second = new AesEncryptPageViewModel(
                new TestMessageService(), NullLogger<DesEncryptPageViewModel>.Instance, store);

            Assert.Equal("待加密的原文", second.Original);
            Assert.Equal((int)OutType.Hex, second.OutTypeIndex);
            Assert.Equal((int)CipherMode.CBC, second.CipherModeIndex);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void ArchiveJson_DoesNotContainSecretOrIvKeys()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = new PageInputStoreService(directory);
            var viewModel = new AesEncryptPageViewModel(
                new TestMessageService(), NullLogger<DesEncryptPageViewModel>.Instance, store);
            viewModel.Original = "敏感键排除验证";
            viewModel.Secret = "不应落盘的密钥";
            viewModel.Iv = "不应落盘的IV向量";
            store.FlushPendingPagesForTests();

            var json = File.ReadAllText(Path.Combine(directory, PageId + ".json"));
            using var document = JsonDocument.Parse(json);
            var inputs = GetInputsObject(document.RootElement);

            // 精确键名按 TryGetProperty 断言，另按不区分大小写扫描全量键名，规避命名策略漂移漏检
            Assert.False(inputs.TryGetProperty("Secret", out _), "存档 JSON 不应包含 Secret 键。");
            Assert.False(inputs.TryGetProperty("Iv", out _), "存档 JSON 不应包含 Iv 键。");

            var keys = inputs.EnumerateObject().Select(property => property.Name).ToList();
            Assert.Contains(keys, key => key.Equals("Original", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(keys, key => key.Equals("Secret", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(keys, key => key.Equals("Iv", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void RestoreAssignment_DoesNotSubmitNewSnapshot()
    {
        var directory = CreateTempDirectory();
        try
        {
            // 通过服务本身预置存档，规避测试手写 JSON 与源生成命名策略漂移
            var store = new PageInputStoreService(directory);
            store.Save(PageId, new Dictionary<string, string?>
            {
                ["Original"] = "已存档内容",
                ["OutTypeIndex"] = "2",
            });
            store.FlushPendingPagesForTests();

            var viewModel = new AesEncryptPageViewModel(
                new TestMessageService(), NullLogger<DesEncryptPageViewModel>.Instance, store);
            Assert.Equal("已存档内容", viewModel.Original);
            Assert.Equal(2, viewModel.OutTypeIndex);

            // 恢复赋值与构造期默认值赋值都不应产生新的待写页（否则默认值快照会覆盖已有存档）
            Assert.Equal(0, store.PendingPageCountForTests);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    // 存档根对象固定为 version/updatedAt/inputs，inputs 键名受命名策略影响，兼容两种大小写读取
    private static JsonElement GetInputsObject(JsonElement root)
    {
        if (root.TryGetProperty("inputs", out var nested))
        {
            return nested;
        }

        return root.GetProperty("Inputs");
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AzrngTools.Tests", "aes-page-input-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
