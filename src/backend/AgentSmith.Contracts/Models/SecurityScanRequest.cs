namespace AgentSmith.Contracts.Models;

/// <summary>
/// Common request model for security scan triggers (webhook, CLI, Slack).
/// All platform-specific payloads map to this record.
/// </summary>
public sealed record SecurityScanRequest(
    string RepoUrl,
    string? PrIdentifier,
    string ProjectName,
    string Platform);
