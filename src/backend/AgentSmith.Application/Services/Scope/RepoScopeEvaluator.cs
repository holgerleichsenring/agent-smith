using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Scope;

/// <summary>
/// p0331/p0386: turns a classifier reply into the run's scope verdict, decided
/// PER REPO. Conservative by construction — a repo is kept when its verdict says
/// affected, when it has no verdict entry, or when its exclusion is below the
/// confidence floor; only a confident exclusion drops it. Unrelated doubt about
/// one repo can no longer void another repo's certain exclusion (the p0331
/// global floor did exactly that). Pure and unit-tested.
/// </summary>
public static class RepoScopeEvaluator
{
    /// <summary>Minimum per-repo exclusion confidence to act on a drop.</summary>
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

        // Configured order is preserved — Repos[0] stays the primary.
        var kept = repos.Where(r => !IsConfidentExclusion(Verdict(classification, r))).ToList();
        var notes = DoubtNotes(classification, repos) + UnknownNote(classification, repos);
        if (kept.Count == 0)
            return (null, KeptAll(all, "classifier excluded every repo" + notes), []);

        var dropped = repos.Except(kept).Select(r => Verdict(classification, r)!).ToList();
        var (expected, expectedNote) = EvaluateExpectedChanges(classification, kept);
        if (dropped.Count == 0)
            return (null,
                $"Ticket scope: all {repos.Count} repos [{all}] affected"
                + $"{Rationale(classification)}{expectedNote}{notes}", expected);
        return (kept,
            $"Ticket scope: narrowed to [{string.Join(", ", kept.Select(r => r.Name))}] of [{all}]"
            + $" — dropped {string.Join("; ", dropped.Select(Describe))}"
            + $"{Rationale(classification)}{expectedNote}{notes}", expected);
    }

    private static RepoScopeVerdict? Verdict(RepoScopeClassification c, RepoConnection repo) =>
        c.Repos.FirstOrDefault(v =>
            string.Equals(v.Name, repo.Name, StringComparison.OrdinalIgnoreCase));

    private static bool IsConfidentExclusion(RepoScopeVerdict? verdict) =>
        verdict is { Affected: false } && verdict.Confidence >= ConfidenceFloor;

    private static string Describe(RepoScopeVerdict v) =>
        $"{v.Name} (confidence {Format(v.Confidence)}"
        + (string.IsNullOrWhiteSpace(v.Reason) ? ")" : $": {v.Reason})");

    // A below-floor exclusion is kept but never silent — the record shows why
    // the classifier's doubt did not narrow the run.
    private static string DoubtNotes(
        RepoScopeClassification c, IReadOnlyList<RepoConnection> repos) =>
        string.Concat(repos
            .Select(r => Verdict(c, r))
            .Where(v => v is { Affected: false } && v.Confidence < ConfidenceFloor)
            .Select(v => $"; kept {v!.Name} — exclusion confidence {Format(v.Confidence)}"
                + $" below floor {Format(ConfidenceFloor)}"));

    // The record is a run artifact — format invariantly, not per host culture.
    private static string Format(double confidence) =>
        confidence.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    // p0386: an entry naming an unknown repo is ignored and noted, never a
    // keep-all trigger — the other verdicts stay actionable.
    private static string UnknownNote(
        RepoScopeClassification c, IReadOnlyList<RepoConnection> repos)
    {
        var unknown = c.Repos
            .Where(v => !repos.Any(r =>
                string.Equals(r.Name, v.Name, StringComparison.OrdinalIgnoreCase)))
            .Select(v => v.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return unknown.Count == 0
            ? string.Empty
            : $"; ignored unknown repo(s) [{string.Join(", ", unknown)}]";
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
