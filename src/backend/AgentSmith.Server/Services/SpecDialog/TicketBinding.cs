using AgentSmith.Contracts.Specs;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-25-8e51b: the ticket a design conversation belongs to, as the conversation stores it.
/// <para>
/// <see cref="Key"/> is the SPEC KEY's spelling — lowercased, every non-alphanumeric character
/// collapsed — because that is how the approval record this conversation will amend is keyed. A
/// raw id would make DPG-1239 and dpg-1239 two conversations on a case-sensitive database and one
/// record, which is the two-drafts problem "one conversation per ticket" exists to remove.
/// </para>
/// <para>
/// <see cref="TicketId"/> is what the tracker calls it, kept because every later call — the
/// fetch, the filing, the amendment — addresses the tracker with its own id, and the key cannot
/// be turned back into one.
/// </para>
/// </summary>
public sealed record TicketBinding(
    string Tracker, string Key, string TicketId, string Title,
    IReadOnlyList<string>? Labels = null)
{
    /// <summary>The binding for a ticket on one tracker connection, keyed as the record is.</summary>
    public static TicketBinding For(
        string tracker, string platform, string ticketId, string title,
        IReadOnlyList<string>? labels = null) =>
        new(tracker, SpecSetKey.For(platform, ticketId).Value, ticketId, title, labels);

    /// <summary>2026-09-25-8e51a: the envelope this ticket routes on — labels, id and platform,
    /// which is everything a ticket read by id can carry.</summary>
    public global::AgentSmith.Contracts.Models.Triggers.IncomingTicketEnvelope Envelope(string platform) =>
        new() { TicketId = TicketId, Platform = platform, Labels = Labels ?? [] };
}
