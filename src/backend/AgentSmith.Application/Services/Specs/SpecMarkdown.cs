using System.Text;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: the spec set rendered for the run detail — the cut, the accounting and
/// the revision list with each revision's cause, so the viewer answers "what is this
/// run working toward, what was left out, and what changed it" without leaving the
/// dashboard. The content of record is still the branch; this is the viewer's copy.
/// <para>
/// p0395: done-criteria render as a titled "Definition of done" list, and each phase's
/// markdown companion is part of the copy; a missing one names the path looked up.
/// </para>
/// </summary>
public static class SpecMarkdown
{
    private const string NoCompanionByApproval =
        "_Approved in the design conversation — the spec is the whole document; it has no ticket companion._";

    public static string Render(SpecSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var sb = new StringBuilder();
        AppendHeader(sb, set);
        AppendPhases(sb, set);
        AppendPhaseDocuments(sb, set);
        AppendAccounting(sb, set);
        AppendRevisions(sb, set);
        return sb.ToString();
    }

    private static void AppendHeader(StringBuilder sb, SpecSet set)
    {
        sb.AppendLine($"# Spec set {set.Key}");
        sb.AppendLine();
        sb.AppendLine(
            $"`{ManifestOf(set)}` — revision {set.Current.Number} ({set.Current.Cause}), "
            + $"source: {Describe(set)}");

        if (set.Handback is { } handback)
        {
            sb.AppendLine();
            sb.AppendLine($"## Handed back — {handback.Case}");
            sb.AppendLine(handback.Reason);
            foreach (var (reading, i) in handback.Readings.Select((r, i) => (r, i)))
                sb.AppendLine($"- {SpecHandbackComment.ReadingLabel(i)} {reading}"
                    + (i == handback.Taken ? " _(taken if nobody answers)_" : string.Empty));
        }
    }

    private static void AppendPhases(StringBuilder sb, SpecSet set)
    {
        sb.AppendLine();
        sb.AppendLine("## Phases");
        foreach (var phase in set.Phases)
        {
            var executed = set.Executed.Contains(phase.PhaseId, StringComparer.Ordinal)
                ? " _(executed)_" : string.Empty;
            sb.AppendLine($"- **{phase.PhaseId}** — {phase.Draft.Goal}{executed}");
            if (phase.Draft.Done.Count == 0) continue;
            sb.AppendLine("  - Definition of done:");
            foreach (var criterion in phase.Draft.Done)
                sb.AppendLine($"    - {criterion}");
        }
    }

    // A blank companion is a failed spec commit (p0394) — unless a named conversation approved the set.
    private static void AppendPhaseDocuments(StringBuilder sb, SpecSet set)
    {
        sb.AppendLine();
        sb.AppendLine("## Phase documents");
        foreach (var phase in set.Phases)
        {
            sb.AppendLine();
            sb.AppendLine($"### {phase.PhaseId} — `{phase.FileStem}.md`");
            sb.AppendLine(DocumentOf(set, phase));
        }
    }

    private static string DocumentOf(SpecSet set, SpecPhase phase) =>
        !string.IsNullOrWhiteSpace(phase.Markdown) ? phase.Markdown.TrimEnd()
        : set.Approval is { Conversation.Length: > 0 } ? NoCompanionByApproval
        : $"_No phase document found — nothing was readable at "
          + $"`{SeriesPaths.Companion(SeriesPaths.Planned, phase.FileStem)}` on the ticket branch._";

    private static void AppendAccounting(StringBuilder sb, SpecSet set)
    {
        sb.AppendLine();
        sb.AppendLine("## Discarded from the ticket");
        sb.AppendLine(set.TicketPinnedWhole
            ? "_The accounting could not be produced — the whole ticket is carried by one phase._"
            : SpecAccountingBuilder.RenderDiscardedForPullRequest(set.Accounting));
    }

    private static void AppendRevisions(StringBuilder sb, SpecSet set)
    {
        sb.AppendLine();
        sb.AppendLine("## Revisions");
        foreach (var revision in set.Revisions)
            sb.AppendLine(
                $"- **{revision.Number}** — {revision.Cause} ({revision.At:yyyy-MM-dd HH:mm} UTC)");
    }

    // 2026-10-06-03c7d: the series' manifest, or the directory manifests lie in before it has one.
    private static string ManifestOf(SpecSet set) =>
        set.Series is { Length: > 0 } seriesBase ? SeriesPaths.Manifest(seriesBase) : SeriesPaths.SeriesRoot + "/";

    private static string Describe(SpecSet set) => set.Source switch
    {
        SpecSource.BranchArtifact => "read back from the ticket branch",
        // 2026-09-17-0e79a: an approved set is not "derived from the ticket" — it names the
        // conversation a person approved it in. 2026-09-22-4d17: that is provenance, not a
        // destination — a change to an approved set is not made in the conversation.
        SpecSource.Approved => "approved in design conversation "
            + (string.IsNullOrWhiteSpace(set.Approval?.Conversation)
                ? "(unnamed)" : set.Approval!.Conversation),
        _ => "derived from the ticket",
    };
}
