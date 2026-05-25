using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// Discovers .agentsmith/contexts/&lt;name&gt;/context.yaml on a remote repo via
/// ISourceProvider (pre-sandbox). One RemoteContextDiscovery per context;
/// empty discovery → one synthetic ("default", ".", null) so un-init / pre-v2
/// repos still get one root sandbox with the generic-image fallback (p0161).
/// </summary>
public sealed class SandboxLanguageResolver(
    ISourceProviderFactory sourceProviderFactory,
    IContextYamlParser contextYamlParser,
    ILogger<SandboxLanguageResolver> logger) : ISandboxLanguageResolver
{
    private const string ContextsRoot = ".agentsmith/contexts";

    private static readonly RemoteContextDiscovery SyntheticDefault =
        new("default", ".", null);

    public async Task<IReadOnlyList<RemoteContextDiscovery>> ResolveAllAsync(
        RepoConnection source, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(source.Url))
            return [SyntheticDefault];

        IReadOnlyList<string> children;
        try
        {
            var provider = sourceProviderFactory.Create(source);
            children = await provider.ListDirectoryAsync(ContextsRoot, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Remote list of {Path} failed for {Url}, falling back to synthetic default", ContextsRoot, source.Url);
            return [SyntheticDefault];
        }

        if (children.Count == 0)
            return [SyntheticDefault];

        var discoveries = new List<RemoteContextDiscovery>();
        foreach (var contextName in children)
        {
            var summary = await TryParseContextYamlAsync(source, contextName, cancellationToken);
            if (summary is null) continue;
            discoveries.Add(new RemoteContextDiscovery(contextName, summary.Workdir, summary.Language));
        }

        return discoveries.Count == 0 ? [SyntheticDefault] : discoveries;
    }

    private async Task<ContextYamlSummary?> TryParseContextYamlAsync(
        RepoConnection source, string contextName, CancellationToken ct)
    {
        var path = $"{ContextsRoot}/{contextName}/context.yaml";
        string? yaml;
        try
        {
            var provider = sourceProviderFactory.Create(source);
            yaml = await provider.TryReadFileAsync(path, ct);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Remote read of {Path} failed for {Url}, skipping context", path, source.Url);
            return null;
        }
        return string.IsNullOrEmpty(yaml) ? null : contextYamlParser.TryParse(yaml);
    }
}
