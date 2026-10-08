using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for fetching a ticket from an external provider.
/// p0322a: TicketId is null on ticketless runs (CLI-triggered init-project) —
/// the handler skips the fetch cleanly instead of failing the step.
/// <para>2026-10-08-e8b9b: CommentKeyword is the tracker trigger's comment_keyword, so the run can
/// read a rework act from the thread whoever claimed the ticket.</para>
/// <para>2026-10-08-2123: TriggerStatuses are the tracker trigger's, so the run can read a person's move
/// back into one of them from the tracker's history.</para>
/// </summary>
public sealed record FetchTicketContext(
    TicketId? TicketId,
    TrackerConnection Config,
    PipelineContext Pipeline,
    string? CommentKeyword = null,
    IReadOnlyList<string>? TriggerStatuses = null) : ICommandContext;
