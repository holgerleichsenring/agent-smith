using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using AgentSmith.Infrastructure.Services.Pocs.Sealing;

namespace AgentSmith.Tests.Pocs.Sealing;

/// <summary>
/// 2026-11-23-933dg: test-only port of the Sealed Secrets controller's HybridDecrypt — split the 2-byte length, the
/// RSA ciphertext and the GCM part; the raw RSA private operation and the EME-OAEP decode (SHA-256, MGF1-SHA256) that
/// CHECKS the label; then AES-256-GCM with an all-zero nonce and no associated data. Throws CryptographicException
/// on anything the controller would refuse.
/// </summary>
internal static class KubeOpener
{
    public static byte[] Open(RSAParameters key, byte[] sealedBytes, byte[] label)
    {
        if (sealedBytes.Length < 2) throw new CryptographicException("too short");
        var rsaLength = BinaryPrimitives.ReadUInt16BigEndian(sealedBytes);
        if (sealedBytes.Length < rsaLength + 2) throw new CryptographicException("too short");
        var sessionKey = Decode(Private(key, sealedBytes.AsSpan(2, rsaLength).ToArray()), label);
        var gcm = sealedBytes.AsSpan(2 + rsaLength);
        var plain = new byte[gcm.Length - 16];
        using var aes = new AesGcm(sessionKey, 16);
        aes.Decrypt(new byte[12], gcm[..plain.Length], gcm[plain.Length..], plain);
        return plain;
    }

    private static byte[] Private(RSAParameters key, byte[] cipher)
    {
        var n = new BigInteger(key.Modulus, isUnsigned: true, isBigEndian: true);
        var m = BigInteger.ModPow(new BigInteger(cipher, isUnsigned: true, isBigEndian: true),
            new BigInteger(key.D, isUnsigned: true, isBigEndian: true), n).ToByteArray(isUnsigned: true, isBigEndian: true);
        var block = new byte[key.Modulus!.Length];
        m.CopyTo(block, block.Length - m.Length);
        return block;
    }

    // RFC 8017 7.1.2 step 3.
    private static byte[] Decode(byte[] block, byte[] label)
    {
        const int h = EmeOaep.HashBytes;
        if (block[0] != 0) throw new CryptographicException("decryption error");
        var seed = block.AsSpan(1, h).ToArray();
        var db = block.AsSpan(1 + h).ToArray();
        EmeOaep.Xor(seed, EmeOaep.Mgf1(db, h));
        EmeOaep.Xor(db, EmeOaep.Mgf1(seed, db.Length));
        if (!db.AsSpan(0, h).SequenceEqual(SHA256.HashData(label))) throw new CryptographicException("label mismatch");
        var one = Array.IndexOf(db, (byte)1, h);
        if (one < 0 || db.AsSpan(h, one - h).ContainsAnyExcept((byte)0)) throw new CryptographicException("decryption error");
        return db[(one + 1)..];
    }
}
