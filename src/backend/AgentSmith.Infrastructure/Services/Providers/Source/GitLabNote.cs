namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>2026-10-08-f147: one merge-request note as the notes API returns it.</summary>
public sealed record GitLabNote(
    long Id, string Body, long? AuthorId, string? AuthorUsername, bool System, DateTimeOffset CreatedAt);
