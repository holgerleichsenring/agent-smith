using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Scope;

/// <summary>
/// p0331: turns a classifier reply into the run's scope verdict. Conservative by
/// construction — every doubtful path (call error, parse failure, low confidence,
/// empty subset, unknown repo name) keeps ALL repos, i.e. today's behavior; only
/// a confident, fully-valid strict subset narrows the run. Pure and unit-tested.
/// </summary>
public static class RepoScopeEvaluator
{
    /// <summary>Minimum classifier confidence to act on a narrowing.</summary>
    public const double ConfidenceFloor = 0.7;

    /// <summary>
    /// <paramref name="error"/> is the classifier-call/parse failure, when any.
    /// Returns the narrowed repo list (null = keep all) plus the human record
    /// line for the run artifact.
    /// </summary>
    public static (IReadOnlyList<RepoConnection>? Scoped, string Record) Evaluate(
        RepoScopeClassification? classification, string? error,
        IReadOnlyList<RepoConnection> repos)
    {
        var all = string.Join(", ", repos.Select(r => r.Name));
        if (error is not null || classification is null)
            return (null, KeptAll(all, error ?? "no classification produced"));
        if (classification.Repos.Count == 0)
            return (null, KeptAll(all, "classifier returned an empty repo list"));
        if (classification.Confidence < ConfidenceFloor)
            return (null, KeptAll(all,
                $"confidence {classification.Confidence:0.00} below floor {ConfidenceFloor:0.00}"
                + Rationale(classification)));

        var scoped = new List<RepoConnection>();
        foreach (var name in classification.Repos.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var match = repos.FirstOrDefault(r =>
                string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
            if (match is null)
                return (null, KeptAll(all, $"classifier named unknown repo '{name}'"));
            scoped.Add(match);
        }

        if (scoped.Count == repos.Count)
            return (null,
                $"Ticket scope: all {repos.Count} repos [{all}] affected"
                + $" (confidence {classification.Confidence:0.00}){Rationale(classification)}");

        // Preserve the configured repo order — Repos[0] stays the primary.
        var ordered = repos.Where(scoped.Contains).ToList();
        return (ordered,
            $"Ticket scope: narrowed to [{string.Join(", ", ordered.Select(r => r.Name))}]"
            + $" of [{all}] (confidence {classification.Confidence:0.00}){Rationale(classification)}");
    }

    private static string KeptAll(string all, string reason) =>
        $"Ticket scope: kept all repos [{all}] — fallback: {reason}";

    private static string Rationale(RepoScopeClassification c) =>
        string.IsNullOrWhiteSpace(c.Rationale) ? string.Empty : $" — {c.Rationale}";
}
