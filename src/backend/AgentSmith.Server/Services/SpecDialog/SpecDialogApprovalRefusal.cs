using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-10-ccd9 review: the approvals refused by the conversation's shape alone — a comment where no ticket is
/// bound, a list of tickets where one is. Deterministic, so the decision path checks them BEFORE it claims, and the
/// sink still refuses them for any caller that did not. 2026-11-15-bf77: and an offer-led proposal the master has
/// not offered to file — File follows the master's offer, and the API holds that for every surface.
/// </summary>
public static class SpecDialogApprovalRefusal
{
    public const string UnboundComment = "A comment is posted on the ticket a conversation belongs to, and this one "
        + "belongs to none. Open Work it out on the ticket to comment on it.";

    public const string ConfigOutsideInstallation = "Configuration changes are applied from the Agent Smith conversation.";

    public const string FilingInsideInstallation = "The Agent Smith conversation changes configuration; it files no tickets.";

    public const string NotOffered = "The design partner has not offered to file this draft. Say that you want it "
        + "filed, and it offers the draft or tells you what is still open.";

    /// <summary>The line the thread is told, or null when the shape allows the approval.</summary>
    public static string? Of(ConversationState state, OutcomeProposal proposal) => proposal switch
    {
        ConfigChangeOutcome when !InstallationScope.Is(state.Project) => ConfigOutsideInstallation, // 2026-11-17-f3a8c
        not ConfigChangeOutcome when InstallationScope.Is(state.Project) => FilingInsideInstallation,
        TicketCommentOutcome when state.TicketKey is null => UnboundComment, // 2026-10-28-7a69c
        TicketGraphOutcome when state.TicketKey is not null => BoundList(state.TicketKey),
        { OfferLed: true, FilingOffer: null } => NotOffered, // 2026-11-15-bf77: File follows the master's offer
        _ => null,
    };

    private static string BoundList(string ticketKey) => $"This conversation belongs to {ticketKey}; approving "
        + "here continues that ticket. File a list of tickets from Work it out without a ticket, or make this one ticket's series.";
}
