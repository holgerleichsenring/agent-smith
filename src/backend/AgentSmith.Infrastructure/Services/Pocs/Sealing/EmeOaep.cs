using System.Security.Cryptography;

namespace AgentSmith.Infrastructure.Services.Pocs.Sealing;

/// <summary>
/// 2026-11-23-933dg: EME-OAEP encoding (RFC 8017 7.1.1) with SHA-256 and MGF1-SHA256 and a LABEL — what Go's
/// rsa.EncryptOAEP does for kubeseal and what .NET's RSAEncryptionPadding cannot express (it takes no label, and the
/// controller refuses an empty-label ciphertext under strict scope). The encoded block is what the RSA public operation
/// raises; nothing here touches a key.
/// </summary>
public sealed class EmeOaep
{
    public const int HashBytes = 32; // not a limit: SHA-256's output size

    /// <summary>The k-byte block 0x00 || maskedSeed || maskedDB for <paramref name="message"/> under <paramref name="label"/>.</summary>
    public byte[] Encode(byte[] message, byte[] label, int k)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Length > k - 2 * HashBytes - 2)
            throw new CryptographicException($"A {message.Length}-byte message does not fit a {k}-byte RSA-OAEP block.");
        var db = new byte[k - HashBytes - 1];
        SHA256.HashData(label).CopyTo(db, 0);
        db[db.Length - message.Length - 1] = 0x01;
        message.CopyTo(db, db.Length - message.Length);
        var seed = RandomNumberGenerator.GetBytes(HashBytes);
        Xor(db, Mgf1(seed, db.Length));
        Xor(seed, Mgf1(db, HashBytes));
        var block = new byte[k];
        seed.CopyTo(block, 1);
        db.CopyTo(block, 1 + HashBytes);
        return block;
    }

    /// <summary>MGF1 with SHA-256: SHA-256(seed || counter) blocks, cut to <paramref name="length"/>.</summary>
    public static byte[] Mgf1(byte[] seed, int length)
    {
        ArgumentNullException.ThrowIfNull(seed);
        var mask = new byte[length];
        var input = new byte[seed.Length + 4];
        seed.CopyTo(input, 0);
        for (var (counter, at) = (0, 0); at < length; counter++, at += HashBytes)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(input.AsSpan(seed.Length), counter);
            var block = SHA256.HashData(input);
            block.AsSpan(0, Math.Min(HashBytes, length - at)).CopyTo(mask.AsSpan(at));
        }
        return mask;
    }

    public static void Xor(byte[] target, byte[] mask)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(mask);
        for (var i = 0; i < target.Length; i++) target[i] ^= mask[i];
    }
}
