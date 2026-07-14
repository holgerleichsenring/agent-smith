using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Scope;

/// <summary>
/// p0336b: turns the classifier's per-repo affected-CONTEXT verdict into a kept
/// set per repo, one level below <see cref="RepoScopeEvaluator"/>. Conservative
/// by construction — every doubtful path (no context verdict, low confidence,
/// unknown context name, empty or full subset, single-context repo) keeps ALL of
/// a kept repo's contexts, so a wrong verdict never sheds a needed sandbox. Only
/// a confident, fully-valid STRICT subset drops contexts. Pure and unit-tested.
/// </summary>
public static class ContextScopeEvaluator
{
    /// <summary>Returns the KEPT contexts per repo (null = keep all, no narrowing)
    /// plus the dropped list for the run record. Repos absent from the kept map
    /// keep all their contexts.</summary>
    public static (IReadOnlyDictionary<string, IReadOnlyList<string>>? Scoped, IReadOnlyList<DroppedContext> Dropped)
        Evaluate(
            RepoScopeClassification? classification, string? error,
            IReadOnlyList<RepoConnection> keptRepos,
            IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>> inventory)
    {
        if (error is not null || classification?.Contexts is null
            || classification.Confidence < RepoScopeEvaluator.ConfidenceFloor)
            return (null, []);

        var scoped = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        var dropped = new List<DroppedContext>();
        foreach (var repo in keptRepos)
            NarrowRepo(repo.Name ?? string.Empty, classification.Contexts, inventory, scoped, dropped);

        return (scoped.Count == 0 ? null : scoped, dropped);
    }

    private static void NarrowRepo(
        string repoName,
        IReadOnlyDictionary<string, IReadOnlyList<string>> affectedByRepo,
        IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>> inventory,
        Dictionary<string, IReadOnlyList<string>> scoped, List<DroppedContext> dropped)
    {
        var all = inventory.TryGetValue(repoName, out var d)
            ? d.Select(x => x.ContextName).ToList() : [];
        if (all.Count <= 1) return; // nothing to narrow
        if (!affectedByRepo.TryGetValue(repoName, out var affected)) return; // no verdict → keep all
        // Unknown context named → distrust the whole verdict for this repo (keep all).
        if (affected.Any(a => !all.Contains(a, StringComparer.OrdinalIgnoreCase))) return;

        var kept = all.Where(c => affected.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();
        if (kept.Count == 0 || kept.Count == all.Count) return; // empty or full → keep all

        scoped[repoName] = kept;
        foreach (var context in all.Where(c => !kept.Contains(c, StringComparer.OrdinalIgnoreCase)))
            dropped.Add(new DroppedContext(repoName, context, "context not affected by the ticket"));
    }
}
