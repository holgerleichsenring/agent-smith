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

    /// <summary>
    /// 2026-09-22-8b25: the reserved phrase a person writes to ask for an approved set to be cut
    /// again — the one deliberate door for somebody who cannot reach the ticket branch. It lives
    /// beside the phrases this system writes here, so one place defines what it is; whether a
    /// comment carries it is <see cref="SpecRecutDemand"/>, which reads it as a WHOLE LINE at the
    /// start of the body rather than anywhere in it.
    /// </summary>
    public const string RecutDemand = "cut this specification again";

    /// <param name="pullRequests">2026-10-12-f5f7e: repository → spec pull request; a repository the
    /// set no longer names is listed as dropped.</param>
    public static string Render(SpecSet set, IReadOnlyDictionary<string, string>? pullRequests)
    {
        ArgumentNullException.ThrowIfNull(set);
        var sb = new StringBuilder();
        sb.AppendLine(Marker);
        sb.AppendLine($"## Agent Smith — {CutMarker}");
        sb.AppendLine();
        sb.AppendLine(SpecSetCommentPreamble.For(set));
        sb.AppendLine();
        sb.Append(SpecRecutNotice.Render(set));
        sb.Append(RenderSpecs(set));
        sb.AppendLine();
        sb.Append(SpecPrBody.RenderDiscarded(set));
        sb.Append(RenderContextsLeftOut(set));
        if (pullRequests is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine(SpecPullRequestLines.Render(pullRequests, set.SeriesRepos, "The specs are open for review"));
        }
        return sb.ToString();
    }

    /// <summary>Each spec with its goal, the criteria it will be held to, the facts it
    /// rests on beside the look each one cites, and what it assumed without looking.</summary>
    private static string RenderSpecs(SpecSet set)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Derived specs");
        sb.AppendLine();
        if (set.Specs.Count == 0)
        {
            sb.AppendLine("_No spec was derived._");
            return sb.ToString();
        }
        foreach (var spec in set.Specs) RenderSpec(sb, spec);
        return sb.ToString();
    }

    // 2026-09-08-1830: the contexts the cut left out, with reasons — shown whenever the
    // cut declared contexts at all, so an author sees a context missing from every spec.
    private static string RenderContextsLeftOut(SpecSet set)
    {
        var declared = set.Specs.Any(p => p.Draft.Contexts.Count > 0);
        if (!declared && set.Accounting.DiscardedContexts.Count == 0) return string.Empty;
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("## Contexts left out");
        sb.AppendLine();
        sb.AppendLine(set.Accounting.DiscardedContexts.Count == 0
            ? "_Every named context is carried by a spec._"
            : SpecAccountingBuilder.RenderDiscardedContexts(set.Accounting));
        return sb.ToString();
    }

    private static void RenderSpec(StringBuilder sb, DerivedSpec spec)
    {
        sb.AppendLine($"### {spec.SpecId} — {spec.Draft.Goal}");
        sb.AppendLine();
        if (spec.Draft.Contexts.Count > 0)
        {
            sb.AppendLine($"**Contexts:** {string.Join(", ", spec.Draft.Contexts)}");
            sb.AppendLine();
        }
        sb.AppendLine("**Done when:**");
        foreach (var criterion in spec.Draft.Done) sb.AppendLine($"- {criterion}");
        if (spec.Draft.Facts.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**Facts (each from a look I took):**");
            foreach (var fact in spec.Draft.Facts)
                sb.AppendLine($"- {fact.Claim}\n  - _{fact.Evidence}_");
        }
        if (spec.Draft.Assumptions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**Assumptions (stated without a look — correct me if wrong):**");
            foreach (var assumption in spec.Draft.Assumptions) sb.AppendLine($"- {assumption}");
        }
        sb.AppendLine();
    }
}
