using System.Text;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-10-14-dbcek: the plan review of a proposed list of tickets — five questions per ticket against what the
/// operator asked, in messages and in uploaded files, and against the repositories. 2026-11-12-cd4ci: advice only, so
/// no quote or replacement; every operator message is shown, and so are the open questions the turn lays on the list.
/// </summary>
public static class TicketPlanReviewPrompt
{
    /// <summary>The key of a finding about the whole list; a ticket key can never take this shape.</summary>
    public const string ListKey = "the list";
    public const string Missing = "missing";
    public const string Owner = "owner";

    public static string For(
        TicketGraphOutcome graph, IReadOnlyList<SpecDialogTurn> transcript, DerivationLook? look, IReadOnlyList<string> uploads,
        IReadOnlyList<OpenQuestion> questions)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(questions);
        return $$"""
            A design conversation proposes a LIST OF TICKETS joined by requires. Each ticket is built by its own run, which writes its specs when it starts, and is merged as its own pull request. Review the list as a PLAN, never spec by spec. For each ticket answer, against the operator's requirements and the repositories:
            1. merge — can it be merged on its own and leave the code working?
            2. order — is it ordered after every ticket whose work it needs?
            3. {{Missing}} — does the list cover every requirement the operator stated? A requirement met by a ticket:<id> requirement, one the conversation leaves for a later list, or one an open question below asks the operator to leave, is not missing.
            4. size — is it small enough for one run? In the field, tickets of one small slice took 15 to 28 minutes; tickets holding a whole stage of a plan failed. Say into which pieces a ticket too large splits.
            5. {{Owner}} — does it rely on a decision the material leaves to its owner that no open question below asks? Write in why the question the operator should be asked.

            Answer with JSON and nothing else:
              [{"ticket": "<key>", "problem": "merge|order|{{Missing}}|size|{{Owner}}|other", "why": "<one sentence>"}]
            A requirement no ticket covers names no ticket: write "ticket": "{{ListKey}}". An empty array means the plan holds.
            {{Uploads(uploads)}}{{DerivationLookPromptSection.Render(look)}}{{Questions(questions)}}
            THE OPERATOR'S REQUIREMENTS (their messages, oldest first)
            {{Requirements(transcript)}}

            THE LIST
            {{Tickets(graph)}}
            """;
    }

    /// <summary>Every message the person wrote, oldest first.</summary>
    public static string Requirements(IReadOnlyList<SpecDialogTurn> transcript)
    {
        ArgumentNullException.ThrowIfNull(transcript);
        return string.Join("\n---\n", transcript.Where(t => t.Role == SpecDialogTurn.UserRole).Select(t => t.Text));
    }

    private static string Uploads(IReadOnlyList<string> uploads) => uploads.Count == 0 ? string.Empty
        : "\nThe operator also uploaded material, readable with the tools under these names; requirements live there too:\n"
          + string.Join("\n", uploads.Select(u => $"- {u}")) + "\n";

    private static string Questions(IReadOnlyList<OpenQuestion> questions)
    {
        if (questions.Count == 0) return string.Empty;
        var sb = new StringBuilder("\nQUESTIONS THE OPERATOR STILL ANSWERS (judge the list as if each proposal is taken)\n");
        foreach (var question in questions)
            sb.AppendLine($"- {question.Question.Trim()}").AppendLine($"  proposed: {question.Proposal.Trim()}");
        return sb.ToString();
    }

    private static string Tickets(TicketGraphOutcome graph) => string.Join("\n\n", graph.Tickets.Select(t =>
        $"key: {t.Key}\ntitle: {t.Title}\nrequires: {(t.Requires.Count == 0 ? "nothing" : string.Join(", ", t.Requires))}\n"
        + (t.Repos.Count == 0 ? string.Empty : $"repos: {string.Join(", ", t.Repos)}\n")
        + $"requirement:\n{t.Requirement.TrimEnd()}\ndone:\n{string.Join("\n", t.Done.Select(d => $"- {d}"))}"));
}
