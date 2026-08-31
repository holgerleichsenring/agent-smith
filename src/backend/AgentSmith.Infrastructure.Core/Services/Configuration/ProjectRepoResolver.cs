using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// Resolves one project's <c>repos:</c> list into materialized <see cref="RepoConnection"/>
/// entries — extracted from <see cref="ResolvedProjectBuilder"/>, whose reason to change was
/// otherwise every reference form a repos entry can take.
/// <para>
/// p0281a/p0285: an entry with a '/' is a connection reference — a wildcard-free include
/// resolves STATICALLY, a wildcard or exclude keeps the discovery path, and a bare name is
/// a legacy repos: catalog entry. All three forms coexist in one project.
/// </para>
/// </summary>
public sealed class ProjectRepoResolver(IConnectionRepoUrlBuilder urlBuilder)
{
    public IReadOnlyList<RepoConnection>? Resolve(
        string project, IReadOnlyList<RawRepoRef> repoEntries,
        IReadOnlyDictionary<string, RepoConnection> repos,
        IReadOnlyDictionary<string, ResolvedConnection> connections,
        RepoGlobExpander? globExpander, List<StartupFinding> findings)
    {
        if (repoEntries.Count == 0)
        {
            findings.Add(ProjectFindings.Blocking(project, "repos",
                $"Project '{project}': 'repos' must list at least one repo (catalog name or connection/glob)."));
            return null;
        }

        var connectionRefs = repoEntries.Where(e => RepoGlobRef.IsConnectionRef(e.Ref)).ToList();
        var resolved = ResolveLegacyRepos(
            project, [.. repoEntries.Where(e => !RepoGlobRef.IsConnectionRef(e.Ref))], repos, findings);
        if (resolved is null) return null;

        var exact = connectionRefs.Where(e => IsExactRef(e.Ref)).ToList();
        var globEntries = connectionRefs.Where(e => !IsExactRef(e.Ref)).ToList();

        if (!ResolveExactRefs(project, exact, connections, resolved, findings)) return null;
        if (!ResolveGlobRefs(project, globEntries, connections, globExpander, resolved, findings)) return null;

        return resolved;
    }

    private static bool IsExactRef(string entry) =>
        RepoGlobRef.Parse(entry) is { IsExclude: false, IsGlob: false };

    private bool ResolveExactRefs(
        string project, IReadOnlyList<RawRepoRef> exact,
        IReadOnlyDictionary<string, ResolvedConnection> connections,
        List<RepoConnection> resolved, List<StartupFinding> findings)
    {
        var anyError = false;
        foreach (var entry in exact)
        {
            var parsed = RepoGlobRef.Parse(entry.Ref);
            if (!connections.TryGetValue(parsed.Connection, out var connection))
            {
                findings.Add(ProjectFindings.Blocking(project, "repos",
                    $"Project '{project}': repo reference uses connection '{parsed.Connection}' which is not " +
                    "defined in connections: catalog."));
                anyError = true;
                continue;
            }
            resolved.Add(urlBuilder.Build(connection, parsed.Pattern, entry.DefaultBranch)
                with { Consumes = entry.Consumes });
        }
        return !anyError;
    }

    private static bool ResolveGlobRefs(
        string project, IReadOnlyList<RawRepoRef> globEntries,
        IReadOnlyDictionary<string, ResolvedConnection> connections,
        RepoGlobExpander? globExpander, List<RepoConnection> resolved, List<StartupFinding> findings)
    {
        if (globEntries.Count == 0) return true;
        // 2026-08-30-c6ec: a wildcard names a set nobody enumerated, while a consumer
        // declaration is a claim about ONE checkout's call sites.
        if (globEntries.Any(e => !string.IsNullOrWhiteSpace(e.Consumes)))
        {
            findings.Add(ProjectFindings.Blocking(project, "repos",
                $"Project '{project}': 'consumes' requires an exact repo reference — a wildcard " +
                "entry cannot declare what it consumes."));
            return false;
        }
        if (globExpander is null)
        {
            findings.Add(ProjectFindings.Blocking(project, "repos",
                $"Project '{project}': connection/glob repo references require repo discovery, " +
                "which is not available in this context."));
            return false;
        }

        var globRefs = globEntries.Select(e => RepoGlobRef.Parse(e.Ref)).ToList();
        resolved.AddRange(globExpander.Expand(project, globRefs, connections));
        return true;
    }

    private static List<RepoConnection>? ResolveLegacyRepos(
        string project, IReadOnlyList<RawRepoRef> entries,
        IReadOnlyDictionary<string, RepoConnection> repos, List<StartupFinding> findings)
    {
        var resolved = new List<RepoConnection>(entries.Count);
        var anyMissing = false;
        foreach (var entry in entries)
        {
            if (repos.TryGetValue(entry.Ref, out var r))
            {
                resolved.Add(r with { Consumes = entry.Consumes });
                continue;
            }
            findings.Add(ProjectFindings.Blocking(project, "repos",
                $"Project '{project}': references repo '{entry.Ref}' which is not defined in repos: catalog."));
            anyMissing = true;
        }
        return anyMissing ? null : resolved;
    }
}
