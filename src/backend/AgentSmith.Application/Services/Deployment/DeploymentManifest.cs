using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;

namespace AgentSmith.Application.Services.Deployment;

/// <summary>2026-11-18-3863c: an environment's manifest as it stands on its repository's default branch, and the
/// version it carries — read without a checkout.</summary>
public sealed class DeploymentManifest(ISourceProviderFactory sources, ManifestVersionKey keys)
{
    /// <summary>The manifest text and its current version, or null with the reason.</summary>
    public async Task<(string? Text, string? Version, string? Error)> ReadAsync(
        RepoConnection repo, DeclaredEnvironment environment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(environment);
        var text = await sources.Create(repo).TryReadFileAsync(environment.Manifest, cancellationToken);
        if (text is null)
            return (null, null, $"`{repo.Name}` has no `{environment.Manifest}` on its default branch "
                + $"(the manifest of environment '{environment.Name}')");
        var (version, error) = keys.Read(text, environment.VersionKey);
        return error is null
            ? (text, version, null)
            : (null, null, $"`{environment.Manifest}` of `{repo.Name}`: {error}");
    }
}
