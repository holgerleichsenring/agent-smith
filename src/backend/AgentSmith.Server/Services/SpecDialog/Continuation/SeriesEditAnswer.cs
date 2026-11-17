namespace AgentSmith.Server.Services.SpecDialog.Continuation;

/// <summary>2026-11-17-dd05c: whether a series edit was recorded for the next attempt, and the sentence that says so.</summary>
public sealed record SeriesEditAnswer(bool Recorded, string Message);
