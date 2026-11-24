using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-23-933dg: how a PoC target receives its PoCs' secret variables and pull secret, from
/// <c>pocs.targets[].secrets:</c>. <see cref="Provider"/> names the provider that writes them on use — Sealed Secrets
/// first, encrypted with the controller's public certificate (<see cref="SealedSecretsCert"/>, a plain field: it is
/// public), which can be fetched from <see cref="SealedSecretsCertUrl"/> into the form.
/// </summary>
public sealed class PocSecretsConfig
{
    public const string Key = "pocs.targets[].secrets";
    public const string SealedSecrets = "sealed_secrets";
    public const string CertKey = Key + ".sealed_secrets_cert";

    public string Provider { get; set; } = SealedSecrets;

    /// <summary>The Sealed Secrets controller's public certificate, PEM (kubeseal --fetch-cert).</summary>
    public string? SealedSecretsCert { get; set; }

    /// <summary>Where the dashboard's Fetch reads the certificate from (https); nothing reads it at run time.</summary>
    public string? SealedSecretsCertUrl { get; set; }

    /// <summary>What is wrong with these values, one sentence each naming the key, the value and the reason.</summary>
    public IEnumerable<string> Problems(string target)
    {
        if (!string.Equals(Provider, SealedSecrets, StringComparison.Ordinal))
            yield return $"{Key}.provider of target '{target}' is '{Provider}'; it must be '{SealedSecrets}', the one secret "
                + "provider agent smith writes so far";
        if (!string.IsNullOrWhiteSpace(SealedSecretsCert) && SealingCertificate.Problem(SealedSecretsCert) is { } problem)
            yield return $"{CertKey} of target '{target}' {problem}";
        if (!string.IsNullOrWhiteSpace(SealedSecretsCertUrl)
            && !(Uri.TryCreate(SealedSecretsCertUrl, UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps))
            yield return $"{Key}.sealed_secrets_cert_url of target '{target}' is '{SealedSecretsCertUrl}'; it must be an https "
                + "URL, because the certificate the PoCs are sealed with must not be read over a channel anyone can change";
    }
}
