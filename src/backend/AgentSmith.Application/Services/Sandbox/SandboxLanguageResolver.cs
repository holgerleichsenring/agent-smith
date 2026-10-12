using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// Discovers .agentsmith/contexts/&lt;name&gt;/context.yaml through the source's own
/// ISourceProvider (pre-sandbox) — a remote and a local working copy alike, since
/// the provider is what knows how to reach either. One RemoteContextDiscovery per
/// context; empty discovery → one synthetic ("default", ".", null) so un-init /
/// pre-v2 repos still get one root sandbox with the generic-image fallback (p0161).
/// <para>
/// 2026-10-12-6bfaa: the branch every context.yaml is read on is the one
/// <see cref="ContextBranchLocator"/> picks from the run's candidates, and each discovery
/// carries where it was read — the synthetic default carries why nothing was.
/// </para>
/// </summary>
public sealed class SandboxLanguageResolver(
    ISourceProviderFactory sourceProviderFactory,
    ContextBranchLocator branchLocator,
    ContextYamlRemoteReader yamlReader,
    ILogger<SandboxLanguageResolver> logger) : ISandboxLanguageResolver
{
    public async Task<IReadOnlyList<RemoteContextDiscovery>> ResolveAllAsync(
        RepoConnection source, ContextReadBranches branches, CancellationToken cancellationToken)
    {
        var listing = await ListContextsAsync(source, branches, cancellationToken);
        if (listing.Contexts.Count > 0)
        {
            logger.LogInformation(
                "Discovery {Repo}: resolved {Count} context(s) [{Contexts}] ({Where})",
                FormatRepoTag(source), listing.Contexts.Count,
                string.Join(", ", listing.Contexts.Select(d => d.ContextName)), listing.Searched);
            return listing.Contexts;
        }

        var reason = listing.UnreadableReason is null
            ? $"no context.yaml under {ContextBranchLocator.ContextsRoot}/ ({listing.Searched ?? "the repository names no url or path"})"
            : $"{ContextBranchLocator.ContextsRoot}/ could not be read ({listing.UnreadableReason})";
        logger.LogWarning(
            "Discovery {Repo}: {Reason}. Using synthetic default (name=default workdir=. lang=null).",
            FormatRepoTag(source), reason);
        return [new RemoteContextDiscovery("default", ".", null, Provenance: reason)];
    }

    public async Task<RemoteContextListing> ListContextsAsync(
        RepoConnection source, ContextReadBranches branches, CancellationToken cancellationToken)
    {
        if (!source.HasLocation) return RemoteContextListing.None;

        var repoTag = FormatRepoTag(source);
        var provider = sourceProviderFactory.Create(source);
        ContextDirectory directory;
        try
        {
            directory = await branchLocator.LocateAsync(provider, branches, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The provider contract lists an absent path as empty and propagates auth and
            // transport errors, so reaching here means the repository could not be read —
            // a different answer from "it declares nothing", and it must stay different.
            logger.LogWarning(ex, "Discovery {Repo}: list of {Path} failed.", repoTag, ContextBranchLocator.ContextsRoot);
            return RemoteContextListing.Unreadable(ex.Message);
        }

        logger.LogInformation(
            "Discovery {Repo}: {Path} on {Where} subfolders=[{Children}] ({Count})",
            repoTag, ContextBranchLocator.ContextsRoot, directory.Where,
            string.Join(", ", directory.Children), directory.Children.Count);

        var discoveries = new List<RemoteContextDiscovery>();
        foreach (var contextName in directory.Children)
        {
            var summary = await yamlReader.ReadAsync(provider, directory.Branch, repoTag, contextName, cancellationToken);
            if (summary is not null)
                discoveries.Add(RemoteContextDiscovery.From(contextName, summary, ProvenanceOf(contextName, directory)));
        }
        return new RemoteContextListing(discoveries, Searched: Describe(directory));
    }

    // p0261: `--context NAME` path — one explicit context, no discovery, no
    // synthetic-default. Reads contexts/NAME/context.yaml for the real toolchain;
    // if that read/parse fails (e.g. the name is wrong, or the local provider has
    // no file there) we still return a NAMED synthetic so the bootstrap probe
    // hits contexts/NAME/ rather than the misleading "default".
    public async Task<IReadOnlyList<RemoteContextDiscovery>> ResolveContextAsync(
        RepoConnection source, string contextName, ContextReadBranches branches, CancellationToken cancellationToken)
    {
        var repoTag = FormatRepoTag(source);
        var provider = sourceProviderFactory.Create(source);
        var directory = await TryLocateAsync(provider, branches, repoTag, cancellationToken);
        var summary = await yamlReader.ReadAsync(provider, directory.Branch, repoTag, contextName, cancellationToken);
        if (summary is not null)
        {
            logger.LogInformation("Context override {Repo}: pinned to '{Context}' (workdir={Workdir} lang={Lang})",
                repoTag, contextName, summary.Workdir, summary.Language ?? "null");
            return [RemoteContextDiscovery.From(contextName, summary, ProvenanceOf(contextName, directory))];
        }
        var reason = $"{ContextBranchLocator.ContextsRoot}/{contextName}/context.yaml not readable on {Describe(directory)}";
        logger.LogWarning(
            "Context override {Repo}/{Context}: {Reason} — using a named synthetic (workdir=. lang=null).",
            repoTag, contextName, reason);
        return [new RemoteContextDiscovery(contextName, ".", null, Provenance: reason)];
    }

    // The override reads one named file wherever it is; a branch that cannot be located is read
    // on the default branch, and that read reports its own failure.
    private async Task<ContextDirectory> TryLocateAsync(
        ISourceProvider provider, ContextReadBranches branches, string repoTag, CancellationToken ct)
    {
        try
        {
            return await branchLocator.LocateAsync(provider, branches, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Context override {Repo}: locating the branch failed.", repoTag);
            return new ContextDirectory(null, [], []);
        }
    }

    private static string ProvenanceOf(string contextName, ContextDirectory directory) =>
        $"{ContextBranchLocator.ContextsRoot}/{contextName}/context.yaml read on {Describe(directory)}";

    private static string Describe(ContextDirectory directory) =>
        directory.PassedOver.Count == 0 ? directory.Where : $"{directory.Where}; {directory.Searched}";

    private static string FormatRepoTag(RepoConnection source)
    {
        var name = string.IsNullOrEmpty(source.Name) ? source.Url ?? source.Path ?? "?" : source.Name;
        var branch = string.IsNullOrEmpty(source.DefaultBranch) ? "auto" : source.DefaultBranch;
        return $"{name}@{branch}";
    }
}
