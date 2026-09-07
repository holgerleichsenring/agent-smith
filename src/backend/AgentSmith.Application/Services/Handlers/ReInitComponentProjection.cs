using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0161d/2026-09-04-0721: what a re-init bootstraps when the repository already carries
/// <c>.agentsmith/contexts/</c> — the components the discovery skill would otherwise have to
/// enumerate, read straight off the sandboxes instead.
/// <para>
/// EVERY context of a sandbox, not the one that names it. A sandbox is one toolchain image, so
/// contexts that share an image share a sandbox and only the first of them is the group's
/// representative. Projecting representatives bootstrapped one context of a two-context
/// repository while the gate went on probing both — the run reported success, the next coding
/// run was refused, and neither message mentioned the other.
/// </para>
/// <para>
/// Its own type because the handler beside it exists to make a model call and parse the answer;
/// deciding what an EXISTING tree already declares needs no model at all.
/// </para>
/// </summary>
internal static class ReInitComponentProjection
{
    public static IReadOnlyDictionary<string, IReadOnlyList<DiscoveredComponent>> PerRepo(
        PipelineContext pipeline,
        IReadOnlyList<RepoConnection> repos,
        IReadOnlyDictionary<string, RemoteContextDiscovery> discoveries,
        SandboxTargets sandboxTargets)
    {
        // p0322b: resolve key→repo through the coordinator's authoritative SandboxRepos map.
        // The old string matcher only knew 'repo' and 'repo/...' — the p0268 multi-group keys
        // fell through, projecting an EMPTY component list, so BootstrapDispatchHandler fanned
        // out ZERO rounds for a multi-context repo on re-init.
        pipeline.TryGet<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos, out var owners);
        var perRepo = new Dictionary<string, IReadOnlyList<DiscoveredComponent>>(
            repos.Count, StringComparer.Ordinal);
        var multiRepo = repos.Count > 1;
        foreach (var repo in repos)
        {
            perRepo[repo.Name] =
            [
                .. discoveries
                    .Where(kv => sandboxTargets.KeyBelongsToRepo(kv.Key, repo.Name, multiRepo, owners))
                    .SelectMany(kv => SandboxContextList
                        .InOr(pipeline, kv.Key, kv.Value)
                        .Select(context => ToComponent(context, kv.Value)))
            ];
        }
        return perRepo;
    }

    // 2026-09-04-0721: a context that declares an image but no stack.lang was legal while it
    // was never projected; projected, an empty slug fails BootstrapDispatch and takes the whole
    // re-init with it. A sandbox is ONE toolchain, so the group's language is the honest answer
    // rather than a guess — and the alternative is a re-init that starts failing on repositories
    // it used to half-serve.
    private static DiscoveredComponent ToComponent(
        RemoteContextDiscovery context, RemoteContextDiscovery representative) =>
        new(context.ContextName, context.Workdir,
            Language(context) ?? Language(representative) ?? string.Empty,
            $"{ProjectMetaPaths.Contexts}/{context.ContextName}/{ProjectMetaPaths.ContextYamlFile}");

    private static string? Language(RemoteContextDiscovery d) =>
        string.IsNullOrWhiteSpace(d.Language) ? null : d.Language;
}
