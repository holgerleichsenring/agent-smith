namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-10-14-dbcej: one ticket of a proposed list. 2026-11-17-5701a: it carries its own reviewed specs
/// (<see cref="Specs"/>), the ticket being their parent. <paramref name="Requires"/> names sibling keys
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

    /// <summary>2026-11-17-5701a: the ticket's specs, each a draft file under tickets/&lt;key&gt;/; empty on a
    /// ticket stored before tickets carried specs.</summary>
    public IReadOnlyList<SpecDraft> Specs { get; init; } = [];
}
