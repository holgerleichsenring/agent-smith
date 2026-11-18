namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-11-17-0207a: one series run — the durable owner of the tickets a Work it out conversation filed. The looked-up
/// columns are columns; the rest of the record (members, question, verdicts, the master's thread, cursors) is one JSON
/// document, written whole under the optimistic <see cref="Version"/>.
/// </summary>
public sealed class SeriesRun : EntityBase
{
    public long Id { get; set; }

    public string SeriesRunId { get; set; } = string.Empty;

    /// <summary>The Work it out conversation's session id.</summary>
    public string SessionId { get; set; } = string.Empty;

    public string Project { get; set; } = string.Empty;

    /// <summary>running, verifying, done or stopped.</summary>
    public string State { get; set; } = string.Empty;

    public string ContentJson { get; set; } = "{}";

    /// <summary>Raised by every save; a save over a stale version is refused and re-applied.</summary>
    public int Version { get; set; }
}
