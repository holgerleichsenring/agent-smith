namespace AgentSmith.Server.Models;

/// <summary>2026-10-13-9a59a: the ticket list's query — project and bucket filters, cursor and page size.</summary>
public sealed record TicketListRequest(string? Project, string? Bucket, string? Cursor, int? Limit)
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    /// <summary>The page size, clamped so a bad or huge limit cannot widen the page.</summary>
    public int PageSize => Math.Clamp(Limit ?? DefaultLimit, 1, MaxLimit);
}
