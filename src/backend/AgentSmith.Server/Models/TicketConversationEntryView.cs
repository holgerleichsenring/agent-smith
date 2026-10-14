using AgentSmith.Domain.Models;

namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-13-9a59h: one entry of a ticket's conversation.
/// </summary>
/// <param name="Id">{source}:{scope}:{sourceId}, unique within the ticket; derived (run id and trail
/// position, or session id and turn index) where the source has no id.</param>
/// <param name="SourceId">The source's own id — a comment id, a pull-request host id, a question id.</param>
/// <param name="PrKey">The pull request's key on a pr-review entry.</param>
/// <param name="Source">One of <see cref="TicketConversationSources"/>.</param>
/// <param name="Role">One of <see cref="TicketConversationRoles"/>.</param>
/// <param name="Author">Only where the source recorded one.</param>
/// <param name="AttemptRunId">The attempt the entry belongs to; null when <paramref name="Group"/> is set.</param>
/// <param name="Group">before (ahead of the first attempt) or now (after the last attempt's approval).</param>
/// <param name="Url">The pull request of a pr-review entry, as the record reads it.</param>
/// <param name="Format">One of <see cref="TicketTextFormat"/>: how <paramref name="Text"/> was written.</param>
public sealed record TicketConversationEntryView(
    string Id,
    string? SourceId,
    string? PrKey,
    string Source,
    string Kind,
    string Role,
    string? Author,
    bool IsOurs,
    DateTimeOffset At,
    string? AttemptRunId,
    string? Group,
    string? Text,
    string? File = null,
    int? Line = null,
    object? Detail = null,
    string? Url = null,
    string Format = TicketTextFormat.Text);
