using AgentSmith.Contracts.Providers;

namespace AgentSmith.Application.Services.Pocs.Build;

/// <summary>
/// 2026-11-23-933dd: the build's questions to the target's registry, through 933de's pull-only reader — whether
/// &lt;repository&gt;/&lt;poc&gt;:&lt;sha&gt; was already pushed (a retry then starts no pipeline), and whether the digest
/// the push job reported is the one the tag resolves to and exists. A tag alone is mutable, so both must agree.
/// </summary>
public sealed class PocBuildRegistry(IContainerRegistryFactory registries)
{
    public Task<string?> PushedAsync(PocBuildJob job, CancellationToken ct)
    {
        var registry = Registry(job);
        return registry.ResolveAsync(registry.Reference(job.Subject.PocName, job.Sha), ct);
    }

    /// <summary>Why <paramref name="digest"/> is not the pushed image, or null when the registry confirms it.</summary>
    public async Task<string?> VerifyAsync(PocBuildJob job, string digest, CancellationToken ct)
    {
        if (!ImageReference.IsDigest(digest)) return $"the push job reported '{digest}', which is not a digest";
        var registry = Registry(job);
        var tagged = registry.Reference(job.Subject.PocName, job.Sha);
        var resolved = await registry.ResolveAsync(tagged, ct);
        var exists = await registry.ExistsAsync(tagged.WithDigest(digest), ct);
        return resolved == digest && exists ? null
            : $"the push job reported {digest}, but the registry resolves {tagged} to {resolved ?? "nothing"}"
              + (exists ? string.Empty : $" and knows no manifest {digest}");
    }

    private IContainerRegistry Registry(PocBuildJob job) =>
        registries.Create(job.Target.Registry
            ?? throw new InvalidOperationException($"pocs.targets[].registry of target '{job.Target.Name}' is missing"));
}
