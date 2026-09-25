namespace AgentSmith.Server.Services.Webhooks;

/// <summary>One pull-request label that asked for a review, plus the project owning the
/// repo it was labelled on — null when no configured repo matched, which is the case the
/// GitHub handler has always accepted (2026-09-25-d83b).</summary>
public sealed record PrTriggerMatch(string? ProjectName);
