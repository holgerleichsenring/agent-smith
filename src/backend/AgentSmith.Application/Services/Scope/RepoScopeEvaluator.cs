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
    /// line for the run artifact. p0384: also returns the validated
    /// expected-changes subset (repos that must CHANGE) — empty on any fallback
    /// or when the classifier omitted / mis-named it, so the keystone keeps its
    /// anyCode semantics unless a confident, fully-valid subset exists.
    /// </summary>
    public static (IReadOnlyList<RepoConnection>? Scoped, string Record, IReadOnlyList<string> ExpectedChanges)
        Evaluate(
            RepoScopeClassification? classification, string? error,
            IReadOnlyList<RepoConnection> repos)
    {
        var all = string.Join(", ", repos.Select(r => r.Name));
        if (error is not null || classification is null)
            return (null, KeptAll(all, error ?? "no classification produced"), []);
        if (classification.Repos.Count == 0)
            return (null, KeptAll(all, "classifier returned an empty repo list"), []);
        if (classification.Confidence < ConfidenceFloor)
            return (null, KeptAll(all,
                $"confidence {classification.Confidence:0.00} below floor {ConfidenceFloor:0.00}"
                + Rationale(classification)), []);

        var scoped = new List<RepoConnection>();
        foreach (var name in classification.Repos.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var match = repos.FirstOrDefault(r =>
                string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
            if (match is null)
                return (null, KeptAll(all, $"classifier named unknown repo '{name}'"), []);
            scoped.Add(match);
        }

        if (scoped.Count == repos.Count)
        {
            var (expected, note) = EvaluateExpectedChanges(classification, repos);
            return (null,
                $"Ticket scope: all {repos.Count} repos [{all}] affected"
                + $" (confidence {classification.Confidence:0.00}){Rationale(classification)}{note}",
                expected);
        }

        // Preserve the configured repo order — Repos[0] stays the primary.
        var ordered = repos.Where(scoped.Contains).ToList();
        var (expectedNarrowed, narrowedNote) = EvaluateExpectedChanges(classification, ordered);
        return (ordered,
            $"Ticket scope: narrowed to [{string.Join(", ", ordered.Select(r => r.Name))}]"
            + $" of [{all}] (confidence {classification.Confidence:0.00}){Rationale(classification)}"
            + narrowedNote,
            expectedNarrowed);
    }

    // p0384: validate expected_changes as a subset of the KEPT repos (canonical
    // spelling from the config). An unknown name drops the WHOLE field — noted
    // on the record, never silent — and the keystone keeps anyCode semantics:
    // the gate must not enforce a requirement the classifier stated incoherently.
    private static (IReadOnlyList<string> Expected, string Note) EvaluateExpectedChanges(
        RepoScopeClassification classification, IReadOnlyList<RepoConnection> keptRepos)
    {
        var claimed = classification.ExpectedChanges;
        if (claimed is null || claimed.Count == 0) return ([], string.Empty);

        var expected = new List<string>();
        foreach (var name in claimed.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var match = keptRepos.FirstOrDefault(r =>
                string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
            if (match is null)
                return ([],
                    $"; expected_changes dropped — '{name}' is not among the kept repos");
            expected.Add(match.Name ?? name);
        }
        return (expected, $"; expected changes: [{string.Join(", ", expected)}]");
    }

    private static string KeptAll(string all, string reason) =>
        $"Ticket scope: kept all repos [{all}] — fallback: {reason}";

    private static string Rationale(RepoScopeClassification c) =>
        string.IsNullOrWhiteSpace(c.Rationale) ? string.Empty : $" — {c.Rationale}";
}
