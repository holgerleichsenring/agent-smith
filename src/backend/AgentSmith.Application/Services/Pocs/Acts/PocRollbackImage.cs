using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;

namespace AgentSmith.Application.Services.Pocs.Acts;

/// <summary>
/// 2026-11-23-c81ed: whether the image a rollback rolls to is still in the target's registry (933de's ExistsAsync with the
/// pull-only token) — the operator's scheduled purge deletes old images, and a manifest naming a gone digest never pulls.
/// Null when it is there; else the sentence naming the revision and the digest.
/// </summary>
public sealed class PocRollbackImage(IContainerRegistryFactory registries)
{
    public async Task<string?> MissingAsync(string poc, int revision, string digest, string? commitSha, PocTargetConfig target, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.Registry is not { } registry) return $"{PocRegistryConfig.Key} of target '{target.Name}' is missing";
        var client = registries.Create(registry);
        return await client.ExistsAsync(client.Reference(poc, commitSha ?? $"r{revision}").WithDigest(digest), ct)
            ? null
            : $"Revision {revision} of '{poc}' cannot be rolled back to: its image {digest} is no longer in {registry.LoginServer} "
              + "(the registry's scheduled purge removed it)";
    }
}
