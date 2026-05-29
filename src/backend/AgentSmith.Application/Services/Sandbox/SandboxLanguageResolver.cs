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

        logger.LogInformation(
            "Discovery: {Url} listed {Path} → found {Count} subfolder(s): [{Children}]",
            source.Url, ContextsRoot, children.Count, string.Join(", ", children));

        if (children.Count == 0)
        {
            logger.LogInformation(
                "Discovery: {Url} has no subfolders under {Path} → falling back to synthetic default ('default','.',null)",
                source.Url, ContextsRoot);
            return [SyntheticDefault];
        }

        var discoveries = new List<RemoteContextDiscovery>();
        foreach (var contextName in children)
        {
            var summary = await TryParseContextYamlAsync(source, contextName, cancellationToken);
            if (summary is null) continue;
            discoveries.Add(new RemoteContextDiscovery(contextName, summary.Workdir, summary.Language));
        }

        if (discoveries.Count == 0)
        {
            logger.LogWarning(
                "Discovery: {Url} found {ChildCount} subfolder(s) but ZERO valid context.yaml → falling back to synthetic default ('default','.',null). " +
                "Run will probe /work/.agentsmith/contexts/default/ which likely does NOT exist on the repo — bootstrap gate will abort.",
                source.Url, children.Count);
            return [SyntheticDefault];
        }

        logger.LogInformation(
            "Discovery: {Url} resolved {Count} valid context(s): [{Contexts}]",
            source.Url, discoveries.Count,
            string.Join(", ", discoveries.Select(d => $"{d.ContextName} (workdir={d.Workdir}, lang={d.Language ?? "null"})")));
        return discoveries;
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
            logger.LogWarning(ex,
                "Discovery: {Url} read of {Path} threw → context '{Context}' skipped",
                source.Url, path, contextName);
            return null;
        }

        if (string.IsNullOrEmpty(yaml))
        {
            logger.LogWarning(
                "Discovery: {Url} read of {Path} returned empty → context '{Context}' skipped",
                source.Url, path, contextName);
            return null;
        }

        ContextYamlSummary? summary;
        try
        {
            summary = contextYamlParser.TryParse(yaml);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Discovery: {Url} parse of {Path} threw ({Reason}) → context '{Context}' skipped",
                source.Url, path, ex.Message, contextName);
            return null;
        }

        if (summary is null)
        {
            logger.LogWarning(
                "Discovery: {Url} parse of {Path} returned null (likely empty/invalid YAML or meta block missing) → context '{Context}' skipped. " +
                "Bytes read: {Bytes}.",
                source.Url, path, contextName, yaml.Length);
            return null;
        }

        logger.LogInformation(
            "Discovery: {Url} parse of {Path} OK → context '{Context}' workdir='{Workdir}' lang='{Lang}'",
            source.Url, path, contextName, summary.Workdir, summary.Language ?? "null");
        return summary;
    }
}
