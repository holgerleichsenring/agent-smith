using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace AgentSmith.Infrastructure.Services.Pocs.Sealing;

/// <summary>
/// 2026-11-23-933dg: kubeseal's HybridEncrypt, in process. A random 32-byte session key is RSA-OAEP-SHA256-encrypted to
/// the controller's key with the strict-scope label '&lt;namespace&gt;/&lt;name&gt;'; the value is AES-256-GCM under the
/// session key with an ALL-ZERO 12-byte nonce (the key is used once) and no associated data, the 16-byte tag appended.
/// The output is the 2-byte big-endian length of the RSA ciphertext, the RSA ciphertext, then the GCM ciphertext.
/// </summary>
public sealed class KubeSealer(EmeOaep oaep, RsaPublicOperation rsa)
{
    private const int SessionKeyBytes = 32; // not a limit: AES-256 key size, as kubeseal draws it
    private const int NonceBytes = 12; // not a limit: AES-GCM nonce size
    private const int TagBytes = 16; // not a limit: AES-GCM tag size

    /// <summary>The strict-scope label a value sealed for <paramref name="name"/> in <paramref name="ns"/> is bound to.</summary>
    public static byte[] Label(string ns, string name) => Encoding.UTF8.GetBytes($"{ns}/{name}");

    public byte[] Seal(RSAParameters key, byte[] plain, byte[] label)
    {
        ArgumentNullException.ThrowIfNull(plain);
        var sessionKey = RandomNumberGenerator.GetBytes(SessionKeyBytes);
        try
        {
            var k = key.Modulus?.Length ?? 0;
            var wrapped = rsa.Apply(key, oaep.Encode(sessionKey, label, k));
            var output = new byte[2 + wrapped.Length + plain.Length + TagBytes];
            BinaryPrimitives.WriteUInt16BigEndian(output, (ushort)wrapped.Length);
            wrapped.CopyTo(output, 2);
            var at = 2 + wrapped.Length;
            using var aes = new AesGcm(sessionKey, TagBytes);
            aes.Encrypt(new byte[NonceBytes], plain, output.AsSpan(at, plain.Length), output.AsSpan(at + plain.Length));
            return output;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(sessionKey);
        }
    }
}
