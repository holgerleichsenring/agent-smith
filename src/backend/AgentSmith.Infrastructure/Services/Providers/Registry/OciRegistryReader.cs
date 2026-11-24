using AgentSmith.Contracts.Exceptions;
using AgentSmith.Contracts.Providers;

namespace AgentSmith.Infrastructure.Services.Providers.Registry;

/// <summary>
/// 2026-11-23-933de: the registry-independent reading of an OCI registry under one repository prefix — probe, resolve a
/// tag, check a digest. A registry type (<see cref="AcrContainerRegistry"/>) adds only its address rules, so a second
/// registry is one small class.
/// </summary>
public sealed class OciRegistryReader(OciDistributionClient client, string repository)
{
    public const string ProbeImage = "agent-smith-probe";
    public const string ProbeTag = "probe";

    public async Task<RegistryProbe> ProbeAsync(CancellationToken cancellationToken)
    {
        var head = await client.HeadManifestAsync($"{repository}/{ProbeImage}", ProbeTag, cancellationToken);
        var place = $"{client.LoginServer}/{repository}";
        return head.State switch
        {
            OciManifestState.TokenRefused => new(RegistryProbeOutcome.TokenRefused, "The registry refused the pull token."),
            OciManifestState.Denied => new(RegistryProbeOutcome.ScopeMissing,
                $"The token signs in but may not pull {repository}/* — the scope map misses it (status {head.Status})."),
            _ => new(RegistryProbeOutcome.Reached, $"The pull token reaches {place}."),
        };
    }

    public async Task<string?> ResolveAsync(ImageReference reference, CancellationToken cancellationToken)
    {
        var tag = reference.Tag ?? throw new ArgumentException($"'{reference}' names no tag to resolve", nameof(reference));
        var head = await HeadAsync(reference, tag, cancellationToken);
        if (head.State == OciManifestState.Missing) return null;
        return ImageReference.IsDigest(head.Digest)
            ? head.Digest
            : throw new ContainerRegistryException(
                $"registry {client.LoginServer} answered {reference.Repository}:{tag} without a sha256 Docker-Content-Digest");
    }

    public async Task<bool> ExistsAsync(ImageReference reference, CancellationToken cancellationToken)
    {
        var digest = reference.Digest
            ?? throw new ArgumentException($"'{reference}' names no digest to check", nameof(reference));
        return (await HeadAsync(reference, digest, cancellationToken)).State == OciManifestState.Found;
    }

    private async Task<OciManifestHead> HeadAsync(ImageReference reference, string at, CancellationToken cancellationToken)
    {
        if (!string.Equals(reference.Registry, client.LoginServer, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"'{reference}' is not on registry {client.LoginServer}", nameof(reference));
        var head = await client.HeadManifestAsync(reference.Repository, at, cancellationToken);
        return head.State is OciManifestState.Found or OciManifestState.Missing
            ? head
            : throw new ContainerRegistryException(head.State == OciManifestState.TokenRefused
                ? $"registry {client.LoginServer} refused the pull token for {reference.Repository}"
                : $"registry {client.LoginServer} denied pulling {reference.Repository} (status {head.Status})");
    }
}
