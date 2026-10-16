using System.Runtime.InteropServices;
using System.Text;

namespace RdmNet.Services;

/// <summary>
/// 使用 Windows DPAPI 加解密密码。密钥由 Windows 按当前用户管理，
/// 密文只能被同一台机器上的同一用户解密。
/// </summary>
public static class DpapiService
{
    private const uint CryptProtectUiForbidden = 0x1;

    /// <summary>加密明文；明文为 null 或空串时返回 null（不调用 DPAPI）。</summary>
    public static string? Protect(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return null;
        }

        byte[] input = Encoding.Unicode.GetBytes(plainText);
        byte[] encrypted = Transform(input, protect: true);
        return Convert.ToBase64String(encrypted);
    }

    /// <summary>解密 base64 密文；返回 null 表示没有密码。</summary>
    public static string? Unprotect(string? base64)
    {
        if (string.IsNullOrEmpty(base64))
        {
            return null;
        }

        byte[] encrypted;
        try
        {
            encrypted = Convert.FromBase64String(base64);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("密码字段不是合法的 base64 数据。", ex);
        }

        if (encrypted.Length == 0)
        {
            return null;
        }

        byte[] plain = Transform(encrypted, protect: false);
        return Encoding.Unicode.GetString(plain);
    }

    private static byte[] Transform(byte[] input, bool protect)
    {
        IntPtr inputPtr = Marshal.AllocHGlobal(input.Length);
        IntPtr descriptionPtr = IntPtr.Zero;
        try
        {
            Marshal.Copy(input, 0, inputPtr, input.Length);

            var inputBlob = new DataBlob { CbData = input.Length, PbData = inputPtr };
            bool succeeded = protect
                ? CryptProtectData(ref inputBlob, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out DataBlob outputBlob)
                : CryptUnprotectData(ref inputBlob, out descriptionPtr, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out outputBlob);

            if (!succeeded)
            {
                throw new InvalidOperationException($"DPAPI 调用失败（错误码 {Marshal.GetLastWin32Error()}）。");
            }

            try
            {
                var result = new byte[outputBlob.CbData];
                Marshal.Copy(outputBlob.PbData, result, 0, outputBlob.CbData);
                return result;
            }
            finally
            {
                if (outputBlob.PbData != IntPtr.Zero)
                {
                    LocalFree(outputBlob.PbData);
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(inputPtr);
            if (descriptionPtr != IntPtr.Zero)
            {
                LocalFree(descriptionPtr);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int CbData;
        public IntPtr PbData;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(
        ref DataBlob pDataIn,
        string? szDataDescr,
        IntPtr pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        uint dwFlags,
        out DataBlob pDataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(
        ref DataBlob pDataIn,
        out IntPtr ppszDataDescr,
        IntPtr pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        uint dwFlags,
        out DataBlob pDataOut);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr hMem);
}
