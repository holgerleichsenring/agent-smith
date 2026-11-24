using System.Security.Cryptography;
using System.Text;
using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs.Sealing;

/// <summary>2026-11-23-933dg: the value of the pod template's agent-smith.io/variables-checksum annotation — SHA-256 hex
/// over the seal fingerprint (or, with none, the sealed files) and the plain variables in name order, each field length-
/// prefixed, so a change to either restarts the pod and nothing else does.</summary>
public static class PocVariablesChecksum
{
    public static string Of(string? sealFingerprint, IReadOnlyList<PocSecretFile> files, IEnumerable<PocPlainVariable> plain)
    {
        var text = new StringBuilder();
        Field(text, sealFingerprint ?? string.Concat(files.Select(f => f.FileName + f.Content)));
        foreach (var variable in plain.OrderBy(v => v.Name, StringComparer.Ordinal))
            Field(Field(text, variable.Name), variable.Value);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    private static StringBuilder Field(StringBuilder text, string value) => text.Append(value.Length).Append(':').Append(value);
}
