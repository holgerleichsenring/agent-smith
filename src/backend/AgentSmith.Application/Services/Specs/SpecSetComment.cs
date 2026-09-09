using System.Text;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-07-b7e2: the body of the derivation-time ticket comment. The author cannot
/// object to what they never see: the cut used to reach them as ids and goals, while
/// the criteria, the facts and the assumptions — the parts a human checks in seconds
/// and the parts that cost money when wrong — stayed on the branch. Information
/// offered, not confirmation demanded: the run proceeds on this comment as before.
/// </summary>
public static class SpecSetComment
{
    /// <summary>Marks the comment as this system's reading, so a human can tell it from an answer.</summary>
    public const string Marker = "<!-- agentsmith:derived-spec -->";

    /// <summary>
    /// 2026-09-08-4aa9: the heading phrase, which is how the next run finds our last cut
    /// comment in the thread — a comment by anyone else after it is the objection the
    /// comment invites, and re-cuts the unstarted tail. A phrase, as the hand-back
    /// comments carry theirs: a tracker may strip an HTML comment, never a heading.
    /// </summary>
    public const string CutMarker = "this is how I understood the ticket";

    public static string Render(SpecSet set, string? pullRequestUrl)
    {
        ArgumentNullException.ThrowIfNull(set);
        var sb = new StringBuilder();
        sb.AppendLine(Marker);
        sb.AppendLine($"## Agent Smith — {CutMarker}");
        sb.AppendLine();
        sb.AppendLine(
            "I split it into the phases below and started working. This is NOT a question and "
            + "the run is not waiting: comment if the cut is wrong and the next run amends an "
            + "unstarted phase or re-cuts the unstarted tail. A phase that already ran is never "
            + "edited — a correction to it becomes a new phase.");
        sb.AppendLine();
        sb.Append(SpecRecutNotice.Render(set));
        sb.Append(RenderPhases(set));
        sb.AppendLine();
        sb.Append(SpecPrBody.RenderDiscarded(set));
        sb.Append(RenderContextsLeftOut(set));
        if (!string.IsNullOrWhiteSpace(pullRequestUrl))
        {
            sb.AppendLine();
            sb.AppendLine($"The specs are open for review: {pullRequestUrl}");
        }
        return sb.ToString();
    }

    /// <summary>Each phase with its goal, the criteria it will be held to, the facts it
    /// rests on beside the look each one cites, and what it assumed without looking.</summary>
    private static string RenderPhases(SpecSet set)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Derived phases");
        sb.AppendLine();
        if (set.Phases.Count == 0)
        {
            sb.AppendLine("_No phase was derived._");
            return sb.ToString();
        }
        foreach (var phase in set.Phases) RenderPhase(sb, phase);
        return sb.ToString();
    }

    // 2026-09-08-1830: the contexts the cut left out, with reasons — shown whenever the
    // cut declared contexts at all, so an author sees a context missing from every phase.
    private static string RenderContextsLeftOut(SpecSet set)
    {
        var declared = set.Phases.Any(p => p.Draft.Contexts.Count > 0);
        if (!declared && set.Accounting.DiscardedContexts.Count == 0) return string.Empty;
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("## Contexts left out");
        sb.AppendLine();
        sb.AppendLine(set.Accounting.DiscardedContexts.Count == 0
            ? "_Every named context is carried by a phase._"
            : SpecAccountingBuilder.RenderDiscardedContexts(set.Accounting));
        return sb.ToString();
    }

    private static void RenderPhase(StringBuilder sb, SpecPhase phase)
    {
        sb.AppendLine($"### {phase.PhaseId} — {phase.Draft.Goal}");
        sb.AppendLine();
        if (phase.Draft.Contexts.Count > 0)
        {
            sb.AppendLine($"**Contexts:** {string.Join(", ", phase.Draft.Contexts)}");
            sb.AppendLine();
        }
        sb.AppendLine("**Done when:**");
        foreach (var criterion in phase.Draft.Done) sb.AppendLine($"- {criterion}");
        if (phase.Draft.Facts.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**Facts (each from a look I took):**");
            foreach (var fact in phase.Draft.Facts)
                sb.AppendLine($"- {fact.Claim}\n  - _{fact.Evidence}_");
        }
        if (phase.Draft.Assumptions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**Assumptions (stated without a look — correct me if wrong):**");
            foreach (var assumption in phase.Draft.Assumptions) sb.AppendLine($"- {assumption}");
        }
        sb.AppendLine();
    }
}
