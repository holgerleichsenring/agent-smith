using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace AgentSmith.Contracts.Pocs;

/// <summary>
/// 2026-11-23-933dg: a Sealed Secrets controller's public certificate, read from PEM — its RSA public key (what a value
/// is sealed to), the SHA-256 fingerprint of its DER bytes and its validity. Only a certificate with an RSA key is one.
/// </summary>
public sealed record SealingCertificate(RSAParameters PublicKey, string Fingerprint, DateTimeOffset NotBefore, DateTimeOffset NotAfter)
{
    /// <summary>The certificate <paramref name="pem"/> holds; a <see cref="FormatException"/> saying why when it holds none.</summary>
    public static SealingCertificate Parse(string pem)
    {
        X509Certificate2 certificate;
        try
        {
            certificate = X509Certificate2.CreateFromPem(pem);
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            throw new FormatException("does not parse as an X.509 certificate in PEM ('-----BEGIN CERTIFICATE-----')", ex);
        }
        using (certificate)
        using (var rsa = certificate.GetRSAPublicKey())
        {
            if (rsa is null) throw new FormatException("holds no RSA public key; Sealed Secrets seals to an RSA key");
            return new SealingCertificate(rsa.ExportParameters(false),
                "sha256:" + Convert.ToHexStringLower(SHA256.HashData(certificate.RawData)),
                certificate.NotBefore.ToUniversalTime(), certificate.NotAfter.ToUniversalTime());
        }
    }

    /// <summary>Why <paramref name="pem"/> is no sealing certificate, as the end of a sentence; null when it is one.</summary>
    public static string? Problem(string pem)
    {
        try
        {
            Parse(pem);
            return null;
        }
        catch (FormatException ex)
        {
            return ex.Message;
        }
    }
}
