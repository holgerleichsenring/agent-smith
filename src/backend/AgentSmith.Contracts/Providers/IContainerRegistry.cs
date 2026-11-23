namespace AgentSmith.Contracts.Providers;

/// <summary>
/// 2026-11-23-933de: one PoC target's container registry, read with its pull-only token. Agent smith never pushes or
/// deletes: the host pipeline pushes (933dd), the operator's scheduled purge deletes. A call that cannot be answered
/// throws <see cref="Exceptions.ContainerRegistryException"/>.
/// </summary>
public interface IContainerRegistry
{
    /// <summary>Whether the pull token may pull under the repository prefix.</summary>
    Task<RegistryProbe> ProbeAsync(CancellationToken cancellationToken);

    /// <summary>The image &lt;login_server&gt;/&lt;repository&gt;/&lt;poc&gt;:&lt;tag&gt;.</summary>
    ImageReference Reference(string poc, string tag);

    /// <summary>The digest a tagged reference points at, or null when the registry knows no such manifest.</summary>
    Task<string?> ResolveAsync(ImageReference reference, CancellationToken cancellationToken);

    /// <summary>Whether the manifest a digest reference names exists.</summary>
    Task<bool> ExistsAsync(ImageReference reference, CancellationToken cancellationToken);
}
