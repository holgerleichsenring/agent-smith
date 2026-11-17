using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.SpecDialog.Writes;
using AgentSmith.Contracts.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-11-15-bf77: offer_filing — the design master offers the operator to file the draft as it stands. The offer
/// lands on the reply slot bound to that draft and the draft is published again, so the composer shows File with the
/// master's line. A later write replaces the draft and so withdraws the offer; a later call replaces it.
/// 2026-11-17-5701a: a ticket list is offered once every ticket carries a spec.
/// </summary>
public sealed class OfferFilingToolHost(SpecDialogReplySlot slot) : IToolHost
{
    public const string Name = "offer_filing";

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? stage, string? investigatorMode)
    {
        _ = stage;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(OfferFiling, name: Name)];
    }

    /// <summary>Whether a rendered master prompt describes the tool, which makes File wait for its offer.</summary>
    public static bool DescribedIn(string? masterBody) =>
        masterBody?.Contains(Name, StringComparison.Ordinal) == true;

    [Description(
        "Offers the operator to file the draft as it stands now. The composer then shows File beside your line, and "
        + "one click files exactly this draft — its open questions decided on your proposals. Your line is the one "
        + "sentence of what filing does, in the language you answer in. The offer belongs to this draft: a change to "
        + "the draft after it withdraws the offer, and calling again offers the draft as it is then.")]
    private string OfferFiling(
        [Description("One sentence in your words of what filing would do.")] string line)
    {
        var draft = slot.Proposal.Current;
        if (draft.IsEmpty) return "Not offered: the draft is empty, so there is nothing to file yet.";
        if (SpecLess(draft) is { } missing) return missing;
        if (string.IsNullOrWhiteSpace(line)) return "Not offered: the line is empty. Say in one sentence what filing does.";
        slot.Offer = new FilingOffer(line.Trim(), draft);
        slot.Proposal.Seed(draft, changed: true); // the offer rides a proposal, so an unchanged draft is published again
        return "Offered. The operator sees File with your line under this reply.";
    }

    private static string? SpecLess(ProposalDraft draft)
    {
        var keys = draft.Tickets.Where(t => t.Specs.Count == 0).Select(t => t.Key).ToList();
        return keys.Count == 0 ? null
            : $"Not offered: ticket(s) '{string.Join("', '", keys)}' carry no spec yet. Write each ticket's specs as "
                + string.Join(", ", keys.Select(k => DraftPath.Shown(DraftPath.Tickets + k + "/<label>.yaml"))) + ".";
    }
}
