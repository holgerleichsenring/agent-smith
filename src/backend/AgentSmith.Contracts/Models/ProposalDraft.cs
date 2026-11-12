namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-11-12-cd4ca: what a design turn's write tools hold — one kind at a time: specs (one, or a parent with its
/// children), tickets, a bug or a comment. Immutable; <see cref="ProposalStore"/> swaps it whole on each write.
/// </summary>
public sealed record ProposalDraft
{
    public static readonly ProposalDraft Empty = new();

    public IReadOnlyList<DraftSpec> Specs { get; init; } = [];

    public IReadOnlyList<GraphTicket> Tickets { get; init; } = [];

    public BugTicketDraft? Bug { get; init; }

    public string? Comment { get; init; }

    /// <summary>The epic's series contract, keyed by the children's spec ids.</summary>
    public Specs.SeriesContract? Contract { get; init; }

    public bool IsEmpty => Specs.Count == 0 && Tickets.Count == 0 && Bug is null && Comment is null;
}
