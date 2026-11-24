namespace AgentSmith.Application.Services.Pocs.Rollout;

/// <summary>2026-11-23-c81ec: one judged poll — wait on, settled or failed — with the progress line it shows and, for a
/// failure, Argo's message.</summary>
public sealed record PocRolloutReading(PocRolloutReadingKind Kind, string Line, string? Message = null);
