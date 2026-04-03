using System.Security.Cryptography;
using System.Text;

namespace SmartSQL.UI.Helpers;

/// <summary>
/// 加密帮助类 - 使用 AES 加密算法
/// </summary>
public static class EncryptHelper
{
    /// <summary>
    /// AES 密钥（256 位）
    /// 在实际生产环境中，此密钥应该存储在更安全的地方（如 Azure Key Vault、AWS Secrets Manager 等）
    /// </summary>
    private static readonly string DefaultKey = "SmartSQL_Aes_Key_2025!@#$%^&*()_+";

    /// <summary>
    /// AES 初始化向量（128 位）
    /// </summary>
    private static readonly string DefaultIV = "SmartSQL_IV_2025";

    /// <summary>
    /// 加密字符串
    /// </summary>
    /// <param name="plainText">明文</param>
    /// <param name="key">密钥（可选，使用默认密钥）</param>
    /// <param name="iv">初始化向量（可选，使用默认 IV）</param>
    /// <returns>加密后的 Base64 字符串</returns>
    public static string Encode(string plainText, string? key = null, string? iv = null)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            return string.Empty;
        }

        try
        {
            var keyBytes = Encoding.UTF8.GetBytes((key ?? DefaultKey).PadRight(32).Substring(0, 32));
            var ivBytes = Encoding.UTF8.GetBytes((iv ?? DefaultIV).PadRight(16).Substring(0, 16));

            using var aes = Aes.Create();
            aes.Key = keyBytes;
            aes.IV = ivBytes;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            return Convert.ToBase64String(cipherBytes);
        }
        catch (Exception)
        {
            // 加密失败时返回原文（降级处理）
            return plainText;
        }
    }

    /// <summary>
    /// 解密字符串
    /// </summary>
    /// <param name="cipherText">Base64 加密字符串</param>
    /// <param name="key">密钥（可选，使用默认密钥）</param>
    /// <param name="iv">初始化向量（可选，使用默认 IV）</param>
    /// <returns>解密后的明文</returns>
    public static string Decode(string cipherText, string? key = null, string? iv = null)
    {
        if (string.IsNullOrWhiteSpace(cipherText))
        {
            return string.Empty;
        }

        try
        {
            var keyBytes = Encoding.UTF8.GetBytes((key ?? DefaultKey).PadRight(32).Substring(0, 32));
            var ivBytes = Encoding.UTF8.GetBytes((iv ?? DefaultIV).PadRight(16).Substring(0, 16));

            using var aes = Aes.Create();
            aes.Key = keyBytes;
            aes.IV = ivBytes;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            var decryptor = aes.CreateDecryptor();
            var cipherBytes = Convert.FromBase64String(cipherText);
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception)
        {
            // 解密失败时返回原文（可能是旧版本未加密的数据）
            return cipherText;
        }
    }
}
