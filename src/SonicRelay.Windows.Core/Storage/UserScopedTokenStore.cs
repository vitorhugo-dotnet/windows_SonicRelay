using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace SonicRelay.Windows.Core.Storage;

public sealed class UserScopedTokenStore : ITokenStore
{
    private readonly string _directory;
    private readonly string _path;
    private readonly ITokenProtector _protector;

    public UserScopedTokenStore(string? directory = null, ITokenProtector? protector = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SonicRelay",
            "WindowsPublisher");
        _path = Path.Combine(_directory, "tokens.dat");
        _protector = protector ?? new WindowsDpapiTokenProtector();
    }

    public async Task<TokenStorageResult> SaveAsync(TokenSet tokens, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        try
        {
            var protectedBytes = _protector.Protect(JsonSerializer.SerializeToUtf8Bytes(tokens));
            Directory.CreateDirectory(_directory);
            var temporaryPath = _path + ".tmp";
            await File.WriteAllBytesAsync(temporaryPath, protectedBytes, cancellationToken);
            File.Move(temporaryPath, _path, true);
            return TokenStorageResult.Success();
        }
        catch (SecureStorageUnavailableException)
        {
            return TokenStorageResult.SecureStorageUnavailable("Secure token storage is unavailable for the current user.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return TokenStorageResult.Failed("Token storage operation failed.");
        }
    }

    public async Task<TokenStorageResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path)) return TokenStorageResult.Success();

        try
        {
            var protectedBytes = await File.ReadAllBytesAsync(_path, cancellationToken);
            var tokens = JsonSerializer.Deserialize<TokenSet>(_protector.Unprotect(protectedBytes));
            return tokens is null
                ? TokenStorageResult.Failed("Stored token data is invalid.")
                : TokenStorageResult.Success(tokens);
        }
        catch (SecureStorageUnavailableException)
        {
            return TokenStorageResult.SecureStorageUnavailable("Secure token storage is unavailable for the current user.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return TokenStorageResult.Failed("Token storage operation failed.");
        }
    }

    public Task<TokenStorageResult> DeleteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            File.Delete(_path);
            return Task.FromResult(TokenStorageResult.Success());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(TokenStorageResult.Failed("Token deletion failed."));
        }
    }
}

public interface ITokenProtector
{
    byte[] Protect(byte[] plaintext);
    byte[] Unprotect(byte[] ciphertext);
}

public sealed class SecureStorageUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);

internal sealed class WindowsDpapiTokenProtector : ITokenProtector
{
    private const int CryptProtectUiForbidden = 0x1;

    public byte[] Protect(byte[] plaintext) => Transform(plaintext, protect: true);
    public byte[] Unprotect(byte[] ciphertext) => Transform(ciphertext, protect: false);

    private static byte[] Transform(byte[] input, bool protect)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new SecureStorageUnavailableException("Windows DPAPI is unavailable.");
        }

        var inputHandle = GCHandle.Alloc(input, GCHandleType.Pinned);
        try
        {
            var inputBlob = new DataBlob(input.Length, inputHandle.AddrOfPinnedObject());
            var succeeded = protect
                ? CryptProtectData(ref inputBlob, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out var outputBlob)
                : CryptUnprotectData(ref inputBlob, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out outputBlob);

            if (!succeeded)
            {
                throw new SecureStorageUnavailableException("Windows DPAPI operation failed.", new Win32Exception(Marshal.GetLastWin32Error()));
            }

            try
            {
                var output = new byte[outputBlob.Length];
                Marshal.Copy(outputBlob.Data, output, 0, output.Length);
                return output;
            }
            finally
            {
                LocalFree(outputBlob.Data);
            }
        }
        finally
        {
            inputHandle.Free();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct DataBlob(int length, IntPtr data)
    {
        public readonly int Length = length;
        public readonly IntPtr Data = data;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref DataBlob input, string? description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);

    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref DataBlob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
