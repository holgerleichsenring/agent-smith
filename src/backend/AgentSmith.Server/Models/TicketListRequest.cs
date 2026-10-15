using AgentSmith.Infrastructure.Persistence.Contracts;

namespace AgentSmith.Server.Models;

/// <summary>2026-10-13-9a59a: the ticket list's query — project and bucket filters, cursor and page size.
/// 2026-10-15-4a4dd: the projects are the caller's, narrowed by a ?project= filter.</summary>
public sealed record TicketListRequest(ProjectScope Projects, string? Bucket, string? Cursor, int? Limit)
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    /// <summary>The page size, clamped so a bad or huge limit cannot widen the page.</summary>
    public int PageSize => Math.Clamp(Limit ?? DefaultLimit, 1, MaxLimit);
}
