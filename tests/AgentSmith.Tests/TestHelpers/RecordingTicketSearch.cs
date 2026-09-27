using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// 2026-09-27-5c1ea: a ticket search as a recorder — what it was asked, with what cap, and a
/// settable answer. The refusal is as much a case as a hit, so the answer is set rather than
/// derived.
/// </summary>
public sealed class RecordingTicketSearch : ITicketSearch
{
    public List<(string Text, int Limit)> Asked { get; } = [];

    public TicketSearchResult Answer { get; set; } = TicketSearchResult.None;

    public Task<TicketSearchResult> SearchAsync(string text, int limit, CancellationToken cancellationToken)
    {
        Asked.Add((text, limit));
        return Task.FromResult(Answer);
    }
}
