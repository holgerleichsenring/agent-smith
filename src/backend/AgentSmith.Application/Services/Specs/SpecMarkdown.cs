using System.Text;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: the spec set rendered for the run detail — the cut, the accounting and
/// the revision list with each revision's cause, so the viewer answers "what is this
/// run working toward, what was left out, and what changed it" without leaving the
/// dashboard. The content of record is still the branch; this is the viewer's copy.
/// </summary>
public static class SpecMarkdown
{
    public static string Render(SpecSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var key = new SpecSetKey(set.Key);
        var sb = new StringBuilder();
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

        sb.AppendLine();
        sb.AppendLine("## Phases");
        foreach (var phase in set.Phases)
        {
            var executed = set.Executed.Contains(phase.PhaseId, StringComparer.Ordinal)
                ? " _(executed)_" : string.Empty;
            sb.AppendLine($"- **{phase.PhaseId}** — {phase.Draft.Goal}{executed}");
            foreach (var criterion in phase.Draft.Done)
                sb.AppendLine($"  - done: {criterion}");
        }

        sb.AppendLine();
        sb.AppendLine("## Discarded from the ticket");
        sb.AppendLine(set.TicketPinnedWhole
            ? "_The accounting could not be produced — the whole ticket is carried by one phase._"
            : SpecAccountingBuilder.RenderDiscardedForPullRequest(set.Accounting));

        sb.AppendLine();
        sb.AppendLine("## Revisions");
        foreach (var revision in set.Revisions)
            sb.AppendLine(
                $"- **{revision.Number}** — {revision.Cause} ({revision.At:yyyy-MM-dd HH:mm} UTC)");
        return sb.ToString();
    }

    private static string Describe(SpecSource source) => source switch
    {
        SpecSource.BranchArtifact => "read back from the ticket branch",
        SpecSource.TicketDescription => "embedded in the ticket description",
        _ => "derived from the ticket",
    };
}
