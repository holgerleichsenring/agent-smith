namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-09-18-c1a7: one row per (Project, TicketId) — the ticket a run could not move out of
/// its trigger status, and the configuration it failed under. The versions are the release:
/// an operator who corrects the configured status bumps the tracker's or the project's
/// document, and a record stamped with the old version no longer stands.
/// </summary>
public sealed class UnmovedTicket : EntityBase
{
    public long Id { get; set; }
    public string Project { get; set; } = string.Empty;
    public string TicketId { get; set; } = string.Empty;

    /// <summary>The tracker catalog key whose document named the status.</summary>
    public string Tracker { get; set; } = string.Empty;

    /// <summary>The status value the run asked for and did not get.</summary>
    public string ConfiguredStatus { get; set; } = string.Empty;

    /// <summary>Which way it failed, as <c>TicketFinalizeOutcome</c>.</summary>
    public int Outcome { get; set; }

    public int TrackerConfigVersion { get; set; }

    public int ProjectConfigVersion { get; set; }
}
