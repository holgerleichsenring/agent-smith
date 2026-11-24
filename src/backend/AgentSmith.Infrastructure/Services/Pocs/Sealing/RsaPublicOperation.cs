using System.Numerics;
using System.Security.Cryptography;

namespace AgentSmith.Infrastructure.Services.Pocs.Sealing;

/// <summary>
/// 2026-11-23-933dg: the raw RSA public operation (RFC 8017 RSAEP, c = m^e mod n) over an encoded block, with
/// BigInteger.ModPow — the step .NET's RSA class only performs behind its own padding. The output is k bytes, big-endian.
/// </summary>
public sealed class RsaPublicOperation
{
    public byte[] Apply(RSAParameters key, byte[] block)
    {
        ArgumentNullException.ThrowIfNull(block);
        if (key.Modulus is null || key.Exponent is null) throw new CryptographicException("The RSA key has no modulus or exponent.");
        var k = key.Modulus.Length;
        var n = new BigInteger(key.Modulus, isUnsigned: true, isBigEndian: true);
        var m = new BigInteger(block, isUnsigned: true, isBigEndian: true);
        if (m >= n) throw new CryptographicException("The encoded block is not below the RSA modulus.");
        var c = BigInteger.ModPow(m, new BigInteger(key.Exponent, isUnsigned: true, isBigEndian: true), n)
            .ToByteArray(isUnsigned: true, isBigEndian: true);
        var output = new byte[k];
        c.CopyTo(output, k - c.Length);
        return output;
    }
}
