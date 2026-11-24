using System.Security.Cryptography;
using System.Text;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Infrastructure.Persistence.Services.Secrets;

/// <summary>
/// 2026-11-23-933dg: the seal fingerprint — HMAC-SHA256 under a key HKDF-derived from the current master key
/// ('as-poc-seal/v1') over the provider, the certificate's fingerprint, the namespace, every secret name and value in
/// name order, pull_mode and, for a pull secret, login server, pull user and token — each field length-prefixed. Keyed,
/// so a stored digest reveals nothing of a short value; it changes when any input does (an environment-held token
/// too), not on a re-wrap; a master-key rotation changes every fingerprint once.
/// </summary>
public sealed class PocSealFingerprint(MasterKeyRing keys) : IPocSealFingerprint
{
    private const string Info = "as-poc-seal/v1";
    private const int KeyBytes = 32; // not a limit: HMAC-SHA256 key size

    public bool IsAvailable => keys.Current is not null;

    public string Of(PocSealInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var master = keys.Current ?? throw new InvalidOperationException("No master key is configured: no seal fingerprint.");
        var key = HKDF.DeriveKey(HashAlgorithmName.SHA256, master.Key, KeyBytes, info: Encoding.UTF8.GetBytes(Info));
        var data = Encoding.UTF8.GetBytes(Canonical(inputs));
        try
        {
            return "hmac-sha256:" + Convert.ToHexStringLower(HMACSHA256.HashData(key, data));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(data);
        }
    }

    private static string Canonical(PocSealInputs inputs)
    {
        var text = new StringBuilder();
        foreach (var field in new[] { inputs.Provider, inputs.CertificateFingerprint, inputs.Namespace, inputs.PullMode })
            Field(text, field);
        foreach (var (name, value) in inputs.Secrets.OrderBy(s => s.Key, StringComparer.Ordinal))
            Field(Field(text, name), value.Reveal());
        foreach (var field in new[] { inputs.LoginServer, inputs.PullUser, inputs.PullToken?.Reveal() })
            Field(text, field ?? "-");
        return text.ToString();
    }

    private static StringBuilder Field(StringBuilder text, string value) => text.Append(value.Length).Append(':').Append(value);
}
