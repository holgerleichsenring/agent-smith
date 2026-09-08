using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Scope;

/// <summary>
/// 2026-09-08-1830: records what the scope call NAMED, so the derivation's cut can be
/// held against it. Distinct from <see cref="ContextScopeEvaluator"/>, which decides
/// what to DROP: run 8688 named both contexts of its one repository, dropped nothing,
/// and was cut for one — the claim has to be on the run whether or not it narrowed.
/// <para>
/// Only names the inventory holds count, and only from repositories with more than one
/// context — a single-context repository has no cut to miss. One repository in scope
/// renders bare names; more render <c>repo/context</c>, so two "api" contexts stay apart.
/// </para>
/// </summary>
public sealed class ScopeNamedContextsRecorder(ILogger<ScopeNamedContextsRecorder> logger)
{
    public void Record(
        PipelineContext pipeline, RepoScopeClassification? classification,
        IReadOnlyList<RepoConnection> keptRepos,
        IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>> inventory)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(keptRepos);
        ArgumentNullException.ThrowIfNull(inventory);
        if (classification?.Contexts is not { } affected) return;
        var qualify = keptRepos.Count > 1;
        var named = keptRepos
            .Select(r => r.Name ?? string.Empty)
            .SelectMany(repo => NamedIn(repo, affected, inventory).Select(c => qualify ? $"{repo}/{c}" : c))
            .ToList();
        if (named.Count == 0) return;
        pipeline.Set(ContextKeys.ScopeNamedContexts, new ScopeNamedContexts(named, classification.Rationale));
        logger.LogInformation("The scope call named context(s) {Contexts}", string.Join(", ", named));
    }

    private static IEnumerable<string> NamedIn(
        string repo, IReadOnlyDictionary<string, IReadOnlyList<string>> affected,
        IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>> inventory)
    {
        if (!inventory.TryGetValue(repo, out var discovered) || discovered.Count <= 1) return [];
        if (!affected.TryGetValue(repo, out var names)) return [];
        return discovered
            .Select(d => d.ContextName)
            .Where(c => names.Contains(c, StringComparer.OrdinalIgnoreCase));
    }
}
