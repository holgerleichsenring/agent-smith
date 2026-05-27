using AgentSmith.Contracts.Events;

namespace AgentSmith.Server.Hubs;

/// <summary>
/// p0169h: paginated trail response. Cursor is the Redis stream entry id
/// of the LAST event in this page; pass it to GetTrailPage as <c>fromId</c>
/// for the next batch. <see cref="HasMore"/> indicates whether the stream
/// has events beyond this page within the retained window.
/// </summary>
public sealed record TrailPage(
    string RunId,
    IReadOnlyList<RunEvent> Events,
    string? NextCursor,
    bool HasMore);
