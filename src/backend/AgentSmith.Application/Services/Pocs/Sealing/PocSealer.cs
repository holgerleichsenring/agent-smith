using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs.Sealing;

/// <summary>
/// 2026-11-23-933dg: turns a seal plan into the result the GitOps commit reads. Nothing to seal → no files and no
/// fingerprint (unchanged when the branch holds none either); something to seal and no certificate → refused naming
/// pocs.targets[].secrets.sealed_secrets_cert; a fingerprint equal to the one the branch holds → unchanged, no files;
/// otherwise poc-secrets (with a secret variable) and poc-pull (with a pull secret), each sealed separately.
/// </summary>
public sealed class PocSealer(IPocSealFingerprint fingerprint)
{
    public (PocSealResult? Result, string? Refusal) Seal(PocSealPlan plan, PocVariableSubject poc, PocTargetConfig target)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(poc);
        ArgumentNullException.ThrowIfNull(target);
        if (!plan.HasAnything) return (Result(poc.SealFingerprint is null, [], null, poc, plan), null);
        if (string.IsNullOrWhiteSpace(target.Secrets.SealedSecretsCert))
            return (null, $"{PocSecretsConfig.CertKey} of target '{target.Name}' is empty; paste the Sealed Secrets controller's "
                + $"public certificate (kubeseal --fetch-cert), because '{poc.Name}' has secrets to seal and none is sealed without it.");
        if (!fingerprint.IsAvailable) return (null, PocSealKey.NeedsMasterKey(target.Name) + ".");
        SealingCertificate certificate;
        try { certificate = SealingCertificate.Parse(target.Secrets.SealedSecretsCert); }
        catch (FormatException ex) { return (null, $"{PocSecretsConfig.CertKey} of target '{target.Name}' {ex.Message}."); }
        var sealFingerprint = fingerprint.Of(plan.Inputs(certificate.Fingerprint));
        if (sealFingerprint == poc.SealFingerprint) return (Result(true, [], sealFingerprint, poc, plan), null);
        return (Result(false, Files(plan, target.Secrets.SealedSecretsCert), sealFingerprint, poc, plan), null);
    }

    private static List<PocSecretFile> Files(PocSealPlan plan, string certificate)
    {
        var files = new List<PocSecretFile>();
        if (plan.Secrets.Count > 0)
            files.Add(plan.Provider.Render(new PocSecretOrder(plan.Namespace, PocSecretNames.Secrets, PocSecretTypes.Opaque, plan.Secrets, certificate)));
        if (plan.PullSecret is { } pull)
            files.Add(plan.Provider.Render(new PocSecretOrder(plan.Namespace, PocSecretNames.Pull, PocSecretTypes.DockerConfigJson,
                new Dictionary<string, Contracts.Models.SecretInput> { [PocSecretNames.DockerConfigJsonKey] = pull }, certificate)));
        return files;
    }

    private static PocSealResult Result(
        bool unchanged, IReadOnlyList<PocSecretFile> files, string? sealFingerprint, PocVariableSubject poc, PocSealPlan plan) =>
        new(unchanged, files, sealFingerprint, PocVariablesChecksum.Of(sealFingerprint, files, poc.Plain))
        {
            HasSecrets = plan.Secrets.Count > 0, NeededFiles = Needed(plan),
        };

    // 2026-11-23-c81eb: the files the GitOps commit keeps on an unchanged seal, in the order Files renders them.
    private static List<string> Needed(PocSealPlan plan) =>
        [.. plan.Secrets.Count > 0 ? [plan.Provider.FileNameOf(PocSecretNames.Secrets)] : Array.Empty<string>(),
            .. plan.PullSecret is null ? [] : new[] { plan.Provider.FileNameOf(PocSecretNames.Pull) }];
}
