using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// 2026-09-28-1da5c: what a tracker links to a ticket, as a recorder — what it was asked, and a
/// settable answer. A refusal is as much a case as a list, so the answer is set rather than derived.
/// </summary>
public sealed class RecordingLinkedWork : ITicketLinkedWork
{
    public List<string> Asked { get; } = [];

    public TicketLinkedWorkResult Answer { get; set; } = TicketLinkedWorkResult.None;

    public Task<TicketLinkedWorkResult> ForAsync(TicketId ticketId, CancellationToken cancellationToken)
    {
        Asked.Add(ticketId.Value);
        return Task.FromResult(Answer);
    }
}
