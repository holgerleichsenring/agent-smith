using System.Text;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-10-14-dbcek: the plan review of a proposed list of tickets — four questions per ticket against what the
/// operator asked, in messages and in uploaded files, and against the repositories. The window over the messages is
/// a code constant pending operator ratification.
/// </summary>
public static class TicketPlanReviewPrompt
{
    /// <summary>The key of a finding about the whole list; a ticket key can never take this shape.</summary>
    public const string ListKey = "the list";
    public const string Missing = "missing";
    public const int RequirementWindowChars = 40_000;

    public static string For(
        TicketGraphOutcome graph, IReadOnlyList<SpecDialogTurn> transcript, DerivationLook? look, IReadOnlyList<string> uploads)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return $$"""
            A design conversation proposes a LIST OF TICKETS joined by requires. Each ticket is built by its own run, which writes its specs when it starts, and is merged as its own pull request. Review the list as a PLAN, never spec by spec. For each ticket answer, against the operator's requirements and the repositories:
            1. merge — can it be merged on its own and leave the code working?
            2. order — is it ordered after every ticket whose work it needs?
            3. missing — does the list cover every requirement the operator stated? A requirement met by a ticket:<id> requirement, or one the conversation says is left for a later list, is not missing.
            4. size — is it small enough for one run? In the field, tickets of one small slice took 15 to 28 minutes; tickets holding a whole stage of a plan failed. A size cut may only remove scope: splitting a ticket changes the keys, and the keys stay.

            Answer with JSON and nothing else:
              [{"ticket": "<key>", "problem": "merge|order|{{Missing}}|size|other", "why": "<one sentence>", "quote": "<verbatim from that ticket's title, requirement or a done item, or null>", "replacement": "<what the quote becomes, \"\" to remove it, or null>"}]
            A cut is applied only when its quote is verbatim in that ticket. A requirement no ticket covers names no ticket: write "ticket": "{{ListKey}}". An empty array means the plan holds.
            {{Uploads(uploads)}}{{DerivationLookPromptSection.Render(look)}}
            THE OPERATOR'S REQUIREMENTS (their messages, oldest first)
            {{Requirements(transcript)}}

            THE LIST
            {{Tickets(graph)}}
            """;
    }

    /// <summary>The person's turns, oldest first; over the window the first turn and the newest that fit.</summary>
    public static string Requirements(IReadOnlyList<SpecDialogTurn> transcript)
    {
        ArgumentNullException.ThrowIfNull(transcript);
        var said = transcript.Where(t => t.Role == SpecDialogTurn.UserRole).Select(t => t.Text).ToList();
        if (said.Sum(s => s.Length) <= RequirementWindowChars) return string.Join("\n---\n", said);
        var first = said[0].Length <= RequirementWindowChars ? said[0] : said[0][..RequirementWindowChars];
        var (kept, room) = (new List<string>(), RequirementWindowChars - first.Length);
        for (var i = said.Count - 1; i > 0 && said[i].Length <= room; i--)
        {
            kept.Insert(0, said[i]);
            room -= said[i].Length;
        }
        var omitted = said.Count - 1 - kept.Count;
        return string.Join("\n---\n", [first, $"({omitted} message(s) between these were left out to fit the window)", .. kept]);
    }

    /// <summary>The cuts to apply, per ticket key, and the shape the re-proposal keeps.</summary>
    public static string CutsNudge(IReadOnlyList<TicketPlanCut> cuts)
    {
        ArgumentNullException.ThrowIfNull(cuts);
        var sb = new StringBuilder("The plan review of your list asks for these changes. Apply each one exactly, in that ticket, and change nothing else:\n");
        foreach (var cut in cuts)
            sb.AppendLine(cut.Replacement.Length == 0
                ? $"- {cut.Ticket}: remove \"{cut.Quote}\" — {cut.Why}"
                : $"- {cut.Ticket}: replace \"{cut.Quote}\" with \"{cut.Replacement}\" — {cut.Why}");
        return sb.Append("Re-propose the whole list, in the same shape, every ticket in it, the keys unchanged.").ToString();
    }

    private static string Uploads(IReadOnlyList<string> uploads) => uploads.Count == 0 ? string.Empty
        : "\nThe operator also uploaded material, readable with the tools under these names; requirements live there too:\n"
          + string.Join("\n", uploads.Select(u => $"- {u}")) + "\n";

    private static string Tickets(TicketGraphOutcome graph) => string.Join("\n\n", graph.Tickets.Select(t =>
        $"key: {t.Key}\ntitle: {t.Title}\nrequires: {(t.Requires.Count == 0 ? "nothing" : string.Join(", ", t.Requires))}\n"
        + (t.Repos.Count == 0 ? string.Empty : $"repos: {string.Join(", ", t.Repos)}\n")
        + $"requirement:\n{t.Requirement.TrimEnd()}\ndone:\n{string.Join("\n", t.Done.Select(d => $"- {d}"))}"));
}
