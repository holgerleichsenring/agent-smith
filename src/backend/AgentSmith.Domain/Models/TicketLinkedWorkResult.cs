namespace AgentSmith.Domain.Models;

/// <summary>One piece of work a tracker links to a ticket.</summary>
/// <param name="Kind">"pull request" or "branch" — the tracker's own idea of it.</param>
/// <param name="Name">What a person would recognise it by: a number, a title, a branch name.</param>
/// <param name="State">What the tracker says it is, where it says anything.</param>
/// <param name="Url">Where a person would open it, where the tracker gives one.</param>
public sealed record TicketLinkedWork(string Kind, string Name, string? State, string? Url);

/// <summary>
/// 2026-09-28-1da5c: what a tracker shows against a ticket, or why it could not say.
/// <para>
/// A tracker that cannot be ASKED, a connection not permitted to read, and a ticket with genuinely
/// no work against it are three different answers. Collapsing them would tell an operator their
/// work does not exist — the reading this series has now removed twice.
/// </para>
/// </summary>
public sealed record TicketLinkedWorkResult(
    IReadOnlyList<TicketLinkedWork> Work, string? Reason)
{
    public bool Answered => Reason is null;

    public static TicketLinkedWorkResult None { get; } = new([], null);

    public static TicketLinkedWorkResult Of(IEnumerable<TicketLinkedWork> work) =>
        new([.. work], null);

    /// <summary>The tracker could not be asked — never to be read as a ticket with no work.</summary>
    public static TicketLinkedWorkResult Refused(string reason) => new([], reason);
}
