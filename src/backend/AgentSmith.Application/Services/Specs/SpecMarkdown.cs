using System.Text;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: the spec set rendered for the run detail — the cut, the accounting and
/// the revision list with each revision's cause, so the viewer answers "what is this
/// run working toward, what was left out, and what changed it" without leaving the
/// dashboard. The content of record is still the branch; this is the viewer's copy.
/// <para>
/// p0395: done-criteria render as a titled "Definition of done" list (the raw yaml
/// key used to leak as a per-line prefix), and each phase's markdown companion is
/// part of the copy — the viewer shows the server-held document instead of pointing
/// at a branch it never reads, and a phase whose document is missing says so
/// explicitly, naming the path that was looked up.
/// </para>
/// </summary>
public static class SpecMarkdown
{
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
        var key = new SpecSetKey(set.Key);
        sb.AppendLine($"# Spec set {set.Key}");
        sb.AppendLine();
        sb.AppendLine(
            $"`{key.Directory}/` — revision {set.Current.Number} ({set.Current.Cause}), "
            + $"source: {Describe(set.Source)}");

        if (set.Handback is { } handback)
        {
            sb.AppendLine();
            sb.AppendLine($"## Handed back — {handback.Case}");
            sb.AppendLine(handback.Reason);
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

    // The per-phase markdown companion, rendered from the set the server holds. A
    // phase without one names the path that was looked up instead of leaving a
    // silently blank section (the spec commit may have failed — see p0394).
    private static void AppendPhaseDocuments(StringBuilder sb, SpecSet set)
    {
        var key = new SpecSetKey(set.Key);
        sb.AppendLine();
        sb.AppendLine("## Phase documents");
        foreach (var phase in set.Phases)
        {
            sb.AppendLine();
            sb.AppendLine($"### {phase.PhaseId} — `{phase.FileStem}.md`");
            sb.AppendLine(string.IsNullOrWhiteSpace(phase.Markdown)
                ? $"_No phase document found — nothing was readable at "
                  + $"`{key.MarkdownPath(phase.FileStem)}` on the ticket branch._"
                : phase.Markdown.TrimEnd());
        }
    }

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

    private static string Describe(SpecSource source) => source switch
    {
        SpecSource.BranchArtifact => "read back from the ticket branch",
        SpecSource.TicketDescription => "embedded in the ticket description",
        _ => "derived from the ticket",
    };
}
