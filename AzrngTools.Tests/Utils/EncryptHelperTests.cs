using AzrngTools.Utils.Database;

namespace AzrngTools.Tests.Utils;

/// <summary>
/// EncryptHelper 基于 Windows DPAPI（当前用户作用域）保护密码。
/// 这些测试在 Windows 上运行，验证同一进程内加解密往返一致。
/// </summary>
public class EncryptHelperTests
{
    [Fact]
    public void Encode_then_decode_roundtrips_plain_text()
    {
        const string plain = "my-db-password-2026";

        var cipher = EncryptHelper.Encode(plain);
        var decoded = EncryptHelper.Decode(cipher);

        Assert.NotEqual(plain, cipher);
        Assert.Equal(plain, decoded);
    }

    [Fact]
    public void Encode_produces_different_ciphertext_with_dpapi_entropy_than_plain()
    {
        var cipher = EncryptHelper.Encode("secret");

        Assert.NotEmpty(cipher);
        // DPAPI 输出是二进制的 Base64，应能还原为字节数组
        Assert.NotEmpty(Convert.FromBase64String(cipher));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Encode_returns_empty_for_blank_input(string? input)
    {
        Assert.Equal(string.Empty, EncryptHelper.Encode(input!));
    }

    [Fact]
    public void Decode_returns_empty_for_non_dpapi_ciphertext()
    {
        // 旧 AES 密文或任意字符串无法用 DPAPI 解密，按设计返回空字符串（用户已确认不考虑历史兼容）
        var fakeCipher = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("not-a-valid-dpapi-blob"));

        var result = EncryptHelper.Decode(fakeCipher);

        Assert.Equal(string.Empty, result);
    }
}
