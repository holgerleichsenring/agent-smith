namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-10-14-dbcej: one ticket TEXT of a proposed list — no spec inside. Its run derives the
/// specs at start against the code merged by then. <paramref name="Requires"/> names sibling keys
/// or <see cref="GraphTicketRequire.ExistingPrefix"/>&lt;id&gt; of a ticket already filed;
/// <paramref name="Repos"/> is empty when the ticket names none.
/// </summary>
public sealed record GraphTicket(
    string Key,
    string Title,
    string Requirement,
    IReadOnlyList<string> Done,
    IReadOnlyList<string> Requires,
    IReadOnlyList<string> Repos)
{
    /// <summary>2026-11-15-67f1: the file text the design master wrote for this ticket; null when no draft file wrote it.</summary>
    public string? Source { get; init; }
}
