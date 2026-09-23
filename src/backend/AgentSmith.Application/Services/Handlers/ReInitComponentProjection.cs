using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0161d/2026-09-04-0721: what a repository already DECLARES under
/// <c>.agentsmith/contexts/</c> — every context of every sandbox it owns, as components.
/// <para>
/// EVERY context of a sandbox, not the one that names it. A sandbox is one toolchain image, so
/// contexts that share an image share a sandbox and only the first of them is the group's
/// representative. Reading representatives stated one context of a two-context repository while
/// the gate went on probing both, so the list at <see cref="ContextKeys.SandboxContexts"/> is
/// what is walked and <see cref="ContextKeys.SandboxDiscoveries"/> answers only where no list
/// was published.
/// </para>
/// <para>
/// 2026-09-23-9bb2: this is PRIOR ART for the discovery round, not its answer. Reading what a
/// tree declares needs no model; deciding whether the declaration STILL HOLDS is exactly what a
/// model is for, and while this stood in for the round a workdir derived wrongly once was
/// returned by every attempt to correct it. A first init declares nothing but the synthetic
/// placeholder, which is filtered here, so the round is told nothing it has to disbelieve.
/// </para>
/// </summary>
internal static class ReInitComponentProjection
{
    private const string SyntheticDefaultName = "default";

    public static IReadOnlyDictionary<string, IReadOnlyList<DiscoveredComponent>> PerRepo(
        PipelineContext pipeline,
        IReadOnlyList<RepoConnection> repos,
        SandboxTargets sandboxTargets)
    {
        // p0322b: resolve key→repo through the coordinator's authoritative SandboxRepos map.
        // The old string matcher only knew 'repo' and 'repo/...' — the p0268 multi-group keys
        // fell through, projecting an EMPTY component list, so BootstrapDispatchHandler fanned
        // out ZERO rounds for a multi-context repo on re-init.
        pipeline.TryGet<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos, out var owners);
        pipeline.TryGet<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
            ContextKeys.SandboxDiscoveries, out var representatives);
        var keys = SandboxKeys(pipeline, representatives);
        var perRepo = new Dictionary<string, IReadOnlyList<DiscoveredComponent>>(
            repos.Count, StringComparer.Ordinal);
        var multiRepo = repos.Count > 1;
        foreach (var repo in repos)
        {
            perRepo[repo.Name] =
            [
                .. keys
                    .Where(key => sandboxTargets.KeyBelongsToRepo(key, repo.Name, multiRepo, owners))
                    .SelectMany(key => Declared(
                        pipeline, key, representatives?.GetValueOrDefault(key)))
            ];
        }
        return perRepo;
    }

    /// <summary>
    /// Every sandbox key that could carry a declaration: the context list's own keys first, and
    /// any key that exists only as a representative — a run whose checkpoint predates the list.
    /// </summary>
    private static IReadOnlyList<string> SandboxKeys(
        PipelineContext pipeline,
        IReadOnlyDictionary<string, RemoteContextDiscovery>? representatives)
    {
        var keys = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (pipeline.TryGet<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
                ContextKeys.SandboxContexts, out var bySandbox) && bySandbox is not null)
            foreach (var key in bySandbox.Keys)
                if (seen.Add(key)) keys.Add(key);
        if (representatives is not null)
            foreach (var key in representatives.Keys)
                if (seen.Add(key)) keys.Add(key);
        return keys;
    }

    private static IEnumerable<DiscoveredComponent> Declared(
        PipelineContext pipeline, string sandboxKey, RemoteContextDiscovery? representative) =>
        SandboxContextList.InOr(pipeline, sandboxKey, representative)
            .Where(IsDeclared)
            .Select(context => ToComponent(context, representative));

    // p0161d: the synthetic default a first init surfaces — no real
    // .agentsmith/contexts/<name>/ existed on the remote. It declares nothing, so it is not
    // prior art; a wrong workdir beside a real language is a declaration and IS.
    private static bool IsDeclared(RemoteContextDiscovery d) =>
        !(d.ContextName == SyntheticDefaultName && d.Workdir == "." && d.Language is null);

    // 2026-09-04-0721: a context that declares an image but no stack.lang states no language of
    // its own. A sandbox is ONE toolchain, so the group's language is the honest answer rather
    // than a guess — and an empty slug reaches the round as a field with nothing in it.
    private static DiscoveredComponent ToComponent(
        RemoteContextDiscovery context, RemoteContextDiscovery? representative) =>
        new(context.ContextName, context.Workdir,
            Language(context) ?? (representative is null ? null : Language(representative)) ?? string.Empty,
            $"{ProjectMetaPaths.Contexts}/{context.ContextName}/{ProjectMetaPaths.ContextYamlFile}");

    private static string? Language(RemoteContextDiscovery d) =>
        string.IsNullOrWhiteSpace(d.Language) ? null : d.Language;
}
