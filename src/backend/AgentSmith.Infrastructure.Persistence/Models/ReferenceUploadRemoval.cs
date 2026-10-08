namespace AgentSmith.Infrastructure.Persistence.Models;

/// <summary>2026-10-08-e8b9g: what became of one upload's removal.</summary>
public enum ReferenceUploadRemoval
{
    Removed,

    /// <summary>The conversation holds no such upload.</summary>
    NotFound,

    /// <summary>An approval of the conversation cites it; nothing was removed.</summary>
    Cited,
}
