namespace AgentSmith.Application.Services.SpecDialog.Writes;

/// <summary>2026-11-15-06fc: what a draft file holds, by its path.</summary>
public enum DraftItemKind
{
    Spec,
    Epic,
    Ticket,
    Bug,
    Comment,
    Contract,

    /// <summary>2026-11-17-5701a: a spec of a ticket, tickets/&lt;key&gt;/&lt;label&gt;.yaml.</summary>
    TicketSpec,
}
