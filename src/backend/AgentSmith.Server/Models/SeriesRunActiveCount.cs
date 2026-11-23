namespace AgentSmith.Server.Models;

/// <summary>2026-11-23-31dcb: how many series the caller may see run right now — the sidebar's counter.</summary>
public sealed record SeriesRunActiveCount(int Running);
