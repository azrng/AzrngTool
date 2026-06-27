using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace AzrngTools.Utils.Database;

/// <summary>
/// 加密帮助类。
/// 使用 Windows DPAPI（<see cref="CryptProtectData"/>，当前用户作用域）保护连接密码等敏感数据。
/// 相比固定 AES 密钥，DPAPI 密钥由操作系统按当前 Windows 用户派生，源码反编译无法解密，
/// 也避免把密钥硬编码在程序集内。
/// </summary>
public static class EncryptHelper
{
    /// <summary>
    /// DPAPI 保护作用域的描述符，同时作为熵盐，降低不同应用间直接复用密文的可能。
    /// </summary>
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("AzrngTools.ConnectionConfig.v1");

    /// <summary>
    /// 加密字符串。
    /// <paramref name="key"/> 与 <paramref name="iv"/> 参数仅为兼容旧调用方保留，DPAPI 模式下忽略。
    /// </summary>
    /// <param name="plainText">明文</param>
    /// <param name="key">已忽略（保留以兼容旧 API）</param>
    /// <param name="iv">已忽略（保留以兼容旧 API）</param>
    /// <returns>Base64 编码的 DPAPI 密文；空输入返回空字符串</returns>
    public static string Encode(string plainText, string? key = null, string? iv = null)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            return string.Empty;
        }

        try
        {
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            if (TryProtect(plainBytes, out var cipherBytes))
            {
                return Convert.ToBase64String(cipherBytes);
            }
        }
        catch (Exception ex)
        {
            LocalLogHelper.LogError($"密码加密失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
        }

        return plainText;
    }

    /// <summary>
    /// 解密字符串。
    /// <paramref name="key"/> 与 <paramref name="iv"/> 参数仅为兼容旧调用方保留，DPAPI 模式下忽略。
    /// </summary>
    /// <param name="cipherText">Base64 编码的 DPAPI 密文</param>
    /// <param name="key">已忽略（保留以兼容旧 API）</param>
    /// <param name="iv">已忽略（保留以兼容旧 API）</param>
    /// <returns>解密后的明文；空输入或解密失败（如旧 AES 密文、跨用户）返回空字符串</returns>
    public static string Decode(string cipherText, string? key = null, string? iv = null)
    {
        if (string.IsNullOrWhiteSpace(cipherText))
        {
            return string.Empty;
        }

        try
        {
            var cipherBytes = Convert.FromBase64String(cipherText);
            if (TryUnprotect(cipherBytes, out var plainBytes))
            {
                return Encoding.UTF8.GetString(plainBytes);
            }
        }
        catch (Exception ex)
        {
            LocalLogHelper.LogError($"密码解密失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
        }

        return string.Empty;
    }

    private static bool TryProtect(byte[] plainBytes, out byte[] cipherBytes)
    {
        cipherBytes = Array.Empty<byte>();
        var plain = CreateDataBlob(plainBytes);
        var entropy = CreateDataBlob(Entropy);
        try
        {
            if (!CryptProtectData(ref plain, null, ref entropy, IntPtr.Zero, IntPtr.Zero, 0, out var cipher))
            {
                return false;
            }

            try
            {
                return TryReadDataBlob(cipher, out cipherBytes);
            }
            finally
            {
                FreeProtectedDataBlob(ref cipher);
            }
        }
        finally
        {
            FreeDataBlob(ref plain);
            FreeDataBlob(ref entropy);
        }
    }

    private static bool TryUnprotect(byte[] cipherBytes, out byte[] plainBytes)
    {
        plainBytes = Array.Empty<byte>();
        var cipher = CreateDataBlob(cipherBytes);
        var entropy = CreateDataBlob(Entropy);
        try
        {
            if (!CryptUnprotectData(ref cipher, out _, ref entropy, IntPtr.Zero, IntPtr.Zero, 0, out var plain))
            {
                return false;
            }

            try
            {
                return TryReadDataBlob(plain, out plainBytes);
            }
            finally
            {
                FreeProtectedDataBlob(ref plain);
            }
        }
        finally
        {
            FreeDataBlob(ref cipher);
            FreeDataBlob(ref entropy);
        }
    }

    private static DataBlob CreateDataBlob(byte[] data)
    {
        var blob = new DataBlob
        {
            cbData = data.Length,
            pbData = Marshal.AllocHGlobal(data.Length)
        };
        Marshal.Copy(data, 0, blob.pbData, data.Length);
        return blob;
    }

    private static bool TryReadDataBlob(DataBlob blob, out byte[] data)
    {
        if (blob.pbData == IntPtr.Zero || blob.cbData <= 0)
        {
            data = Array.Empty<byte>();
            return false;
        }

        data = new byte[blob.cbData];
        Marshal.Copy(blob.pbData, data, 0, blob.cbData);
        return true;
    }

    private static void FreeDataBlob(ref DataBlob blob)
    {
        if (blob.pbData != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(blob.pbData);
            blob.pbData = IntPtr.Zero;
        }
    }

    private static void FreeProtectedDataBlob(ref DataBlob blob)
    {
        if (blob.pbData != IntPtr.Zero)
        {
            LocalFree(blob.pbData);
            blob.pbData = IntPtr.Zero;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int cbData;
        public IntPtr pbData;
    }

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    [SuppressUnmanagedCodeSecurity]
    private static extern bool CryptProtectData(
        ref DataBlob dataIn,
        string? description,
        ref DataBlob entropy,
        IntPtr reserved,
        IntPtr prompt,
        int flags,
        out DataBlob dataOut);

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    [SuppressUnmanagedCodeSecurity]
    private static extern bool CryptUnprotectData(
        ref DataBlob dataIn,
        out string? description,
        ref DataBlob entropy,
        IntPtr reserved,
        IntPtr prompt,
        int flags,
        out DataBlob dataOut);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr handle);
}
