namespace AgentSmith.Contracts.Sweep;

/// <summary>2026-10-08-9e6e: how far a change source has been read, and where a cut read resumes.</summary>
public sealed record SweepPosition(DateTimeOffset At, string? Resume = null);
