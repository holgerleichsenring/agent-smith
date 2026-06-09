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

        var repoTag = FormatRepoTag(source);

        IReadOnlyList<string> children;
        try
        {
            var provider = sourceProviderFactory.Create(source);
            children = await provider.ListDirectoryAsync(ContextsRoot, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Discovery {Repo}: list of {Path} failed. Using synthetic default.",
                repoTag, ContextsRoot);
            return [SyntheticDefault];
        }

        if (children.Count == 0)
        {
            logger.LogWarning(
                "Discovery {Repo}: {Path} is empty. Using synthetic default (name=default workdir=. lang=null). " +
                "Probe will hit /work/.agentsmith/contexts/default/ — typically not on the repo.",
                repoTag, ContextsRoot);
            return [SyntheticDefault];
        }

        logger.LogInformation(
            "Discovery {Repo}: {Path} subfolders=[{Children}] ({Count})",
            repoTag, ContextsRoot, string.Join(", ", children), children.Count);

        var discoveries = new List<RemoteContextDiscovery>();
        foreach (var contextName in children)
        {
            var summary = await TryParseContextYamlAsync(source, repoTag, contextName, cancellationToken);
            if (summary is null) continue;
            discoveries.Add(new RemoteContextDiscovery(
                contextName, summary.Workdir, summary.Language, summary.Prerequisites, summary.Image));
        }

        if (discoveries.Count == 0)
        {
            logger.LogWarning(
                "Discovery {Repo}: {ChildCount} subfolder(s) found but 0 valid context.yaml. Using synthetic default. " +
                "Probe will hit /work/.agentsmith/contexts/default/ — typically not on the repo.",
                repoTag, children.Count);
            return [SyntheticDefault];
        }

        logger.LogInformation(
            "Discovery {Repo}: resolved {Count} context(s) [{Contexts}]",
            repoTag, discoveries.Count,
            string.Join(", ", discoveries.Select(d => d.ContextName)));
        return discoveries;
    }

    private async Task<ContextYamlSummary?> TryParseContextYamlAsync(
        RepoConnection source, string repoTag, string contextName, CancellationToken ct)
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
                "Context {Repo}/{Context}: read {Path} threw. Skipped.",
                repoTag, contextName, path);
            return null;
        }

        if (string.IsNullOrEmpty(yaml))
        {
            logger.LogWarning(
                "Context {Repo}/{Context}: read {Path} returned empty. Skipped.",
                repoTag, contextName, path);
            return null;
        }

        ContextYamlParseResult result;
        try
        {
            result = contextYamlParser.Parse(yaml);
        }
        catch (InvalidOperationException ex)
        {
            // meta.workdir is required — surface as a config error.
            logger.LogWarning(
                "Context {Repo}/{Context}: {Path} rejected — {Reason}. Skipped.",
                repoTag, contextName, path, ex.Message);
            return null;
        }

        if (result.ErrorReason is not null)
        {
            logger.LogWarning(
                "Context {Repo}/{Context}: {Path} parse failed — {Reason}. Skipped (sandbox will fall back to generic image).",
                repoTag, contextName, path, result.ErrorReason);
            return null;
        }

        if (result.Summary is null)
        {
            logger.LogWarning(
                "Context {Repo}/{Context}: {Path} produced no summary (empty or shape did not match expected fields, {Bytes} bytes read). Skipped.",
                repoTag, contextName, path, yaml.Length);
            return null;
        }

        var summary = result.Summary;
        logger.LogInformation(
            "Context {Repo}/{Context}: workdir={Workdir} lang={Lang} image={Image}",
            repoTag, contextName, summary.Workdir, summary.Language ?? "null", summary.Image ?? "null");
        return summary;
    }

    private static string FormatRepoTag(RepoConnection source)
    {
        var name = string.IsNullOrEmpty(source.Name) ? source.Url ?? "?" : source.Name;
        var branch = string.IsNullOrEmpty(source.DefaultBranch) ? "auto" : source.DefaultBranch;
        return $"{name}@{branch}";
    }
}
