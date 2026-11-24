using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Providers;

namespace AgentSmith.Application.Services.Pocs.GitOps;

/// <summary>
/// 2026-11-23-c81eb: what a render reads, gathered — the namespace (933dg's PocNamespace), the host (domain_pattern with
/// the name), the port, source commit and digest of the revision the desired state names (so a rollback to n renders n's
/// port), and the image as 933de's ImageReference by digest — never by tag alone.
/// </summary>
public sealed class PocRenderInputs(IPocGitOpsStore store, IContainerRegistryFactory registries)
{
    public async Task<(PocRenderInput? Input, string? Refusal)> BuildAsync(
        PocGitOpsSubject poc, PocDesiredState desired, PocTargetConfig target, PocSealResult seal, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(poc);
        ArgumentNullException.ThrowIfNull(desired);
        ArgumentNullException.ThrowIfNull(target);
        if (string.IsNullOrWhiteSpace(target.DomainPattern))
            return (null, $"pocs.targets[].domain_pattern of target '{target.Name}' is empty; a PoC's Ingress needs a host made from it.");
        if (target.Registry is not { } registry) return (null, $"{PocRegistryConfig.Key} of target '{target.Name}' is missing.");
        if (await store.RevisionAsync(poc.PocId, desired.Revision, ct) is not { Port: { } port, CommitSha: { } sha })
            return (null, $"Revision {desired.Revision} of '{poc.Name}' has no port or source commit; it was never committed.");
        if (!ImageReference.IsDigest(desired.ImageDigest))
            return (null, $"'{desired.ImageDigest}' is not a sha256 digest; a PoC is deployed by digest only.");
        var image = registries.Create(registry).Reference(poc.Name, sha).WithDigest(desired.ImageDigest).ToString();
        var host = target.DomainPattern.Replace("{app}", poc.Name, StringComparison.Ordinal);
        return (new PocRenderInput(poc.Name, PocNamespace.For(poc.Name), host, port, image, desired, target, seal, poc.Plain), null);
    }
}
