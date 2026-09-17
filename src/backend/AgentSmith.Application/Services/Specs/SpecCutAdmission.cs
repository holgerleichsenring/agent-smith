using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0422: decides which of a cut reviewer's findings the framework keeps. Anything the
/// framework cannot verify is the reviewer inventing a fault, which would block a cut
/// nobody can find the flaw in — so it is discarded and logged.
/// <para>
/// 2026-09-15-ffa7: two shapes. A contradiction, an uncheckable criterion or one the ticket
/// never asked for quotes a criterion the phase states. A FALSE PREMISE cites an id minted
/// in the reviewer's OWN evidence — the rule <see cref="FactResolver"/> states: a line is a
/// fact when the framework minted the id it cites, never because the model labelled it. A
/// path would not do: a reviewer asked for one produces a plausible path it never opened.
/// </para>
/// </summary>
internal sealed class SpecCutAdmission(ILogger logger)
{
    public IReadOnlyList<CutFinding> Admit(
        SpecSet set, IReadOnlyList<CutFinding> answer, DerivationLook? look)
    {
        // Only a look that reached a verdict can prove a premise false; one that could not run
        // keeps its id and its line, and proves nothing.
        var ran = new HashSet<string>(
            (look?.Evidence.Looks ?? []).Where(l => l.Ran).Select(l => l.Id), StringComparer.Ordinal);
        return [.. answer.Where(finding => Admitted(set, finding, ran))];
    }

    private bool Admitted(SpecSet set, CutFinding finding, IReadOnlySet<string> ran)
    {
        var phase = set.Phases.FirstOrDefault(p =>
            string.Equals(p.Draft.PhaseId, finding.PhaseId, StringComparison.OrdinalIgnoreCase));
        if (IsFalsePremise(finding)) return Cited(finding, phase, ran);
        if (phase is not null && phase.Draft.Done.Any(d => Matches(d, finding.Criterion))) return true;

        logger.LogWarning(
            "Cut review quoted a criterion that is not in {Phase} — discarding the finding: {Criterion}",
            finding.PhaseId, Shorten(finding.Criterion));
        return false;
    }

    private bool Cited(CutFinding finding, SpecPhase? phase, IReadOnlySet<string> ran)
    {
        if (phase is not null && DerivationEvidence.CitationsIn(finding.Cites).Any(ran.Contains))
            return true;
        logger.LogWarning(
            "Cut review reported a false premise in {Phase} citing {Cites}, and no such phase or no look of its own that ran minted that id — discarding the finding",
            finding.PhaseId, finding.Cites ?? "nothing");
        return false;
    }

    public static bool IsFalsePremise(CutFinding finding) =>
        string.Equals(finding.Problem?.Trim(), SpecCutVerdicts.FalsePremise, StringComparison.OrdinalIgnoreCase);

    private static bool Matches(string stated, string quoted) =>
        stated.Contains(quoted.Trim(), StringComparison.OrdinalIgnoreCase)
        || quoted.Contains(stated.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string Shorten(string text) => text.Length <= 80 ? text : text[..80] + "…";
}
