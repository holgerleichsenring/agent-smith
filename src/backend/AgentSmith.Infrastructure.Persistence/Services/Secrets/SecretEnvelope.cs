using System.Security.Cryptography;
using System.Text;

namespace AgentSmith.Infrastructure.Persistence.Services.Secrets;

/// <summary>
/// 2026-11-17-f3a8d: envelope encryption, format v1. Every seal draws a fresh 32-byte data key and fresh 12-byte nonces,
/// so equal values never store equal bytes. The value is AES-256-GCM under the data key with 'as-secret/v1\0name' as
/// associated data; the data key is AES-256-GCM under the master key with 'as-dek/v1\0name\0keyId', so neither a
/// ciphertext nor a wrapped key opens under another row's name or another key's id. Key buffers are zeroed after use.
/// </summary>
public sealed class SecretEnvelope
{
    public const int Version = 1; // not a limit: the envelope format number
    private const int KeyBytes = 32; // not a limit: AES-256 key size
    private const int NonceBytes = 12; // not a limit: AES-GCM nonce size
    private const int TagBytes = 16; // not a limit: AES-GCM tag size

    public SealedSecret Seal(string name, string value, MasterKey master)
    {
        var dataKey = RandomNumberGenerator.GetBytes(KeyBytes);
        var plain = Encoding.UTF8.GetBytes(value);
        try
        {
            var (cipher, nonce) = Encrypt(dataKey, plain, ValueData(name));
            var (wrapped, wrapNonce) = Encrypt(master.Key, dataKey, KeyData(name, master.Id));
            return new SealedSecret(cipher, nonce, wrapped, wrapNonce, master.Id);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    /// <summary>The value; a <see cref="CryptographicException"/> when the key, the name or the bytes do not match.</summary>
    public string Open(string name, SealedSecret sealedSecret, MasterKey master)
    {
        var dataKey = Decrypt(master.Key, sealedSecret.WrappedKey, sealedSecret.WrapNonce, KeyData(name, master.Id));
        try
        {
            var plain = Decrypt(dataKey, sealedSecret.Ciphertext, sealedSecret.Nonce, ValueData(name));
            try { return Encoding.UTF8.GetString(plain); }
            finally { CryptographicOperations.ZeroMemory(plain); }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    /// <summary>The same value under another master key: the data key is unwrapped and wrapped again; the value's ciphertext stays.</summary>
    public SealedSecret Rewrap(string name, SealedSecret sealedSecret, MasterKey from, MasterKey to)
    {
        var dataKey = Decrypt(from.Key, sealedSecret.WrappedKey, sealedSecret.WrapNonce, KeyData(name, from.Id));
        try
        {
            var (wrapped, wrapNonce) = Encrypt(to.Key, dataKey, KeyData(name, to.Id));
            return sealedSecret with { WrappedKey = wrapped, WrapNonce = wrapNonce, KeyId = to.Id };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    private static (byte[] Cipher, byte[] Nonce) Encrypt(byte[] key, byte[] plain, byte[] associated)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
        var output = new byte[plain.Length + TagBytes];
        using var aes = new AesGcm(key, TagBytes);
        aes.Encrypt(nonce, plain, output.AsSpan(0, plain.Length), output.AsSpan(plain.Length), associated);
        return (output, nonce);
    }

    private static byte[] Decrypt(byte[] key, byte[] cipher, byte[] nonce, byte[] associated)
    {
        if (cipher.Length < TagBytes) throw new CryptographicException("The stored value is shorter than its tag.");
        var plain = new byte[cipher.Length - TagBytes];
        using var aes = new AesGcm(key, TagBytes);
        aes.Decrypt(nonce, cipher.AsSpan(0, plain.Length), cipher.AsSpan(plain.Length), plain, associated);
        return plain;
    }

    private static byte[] ValueData(string name) => Encoding.UTF8.GetBytes($"as-secret/v{Version}\0{name}");

    private static byte[] KeyData(string name, string keyId) => Encoding.UTF8.GetBytes($"as-dek/v{Version}\0{name}\0{keyId}");
}
