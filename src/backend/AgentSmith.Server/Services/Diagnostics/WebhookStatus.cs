namespace AgentSmith.Server.Services.Diagnostics;

/// <summary>
/// The honest webhook signal per platform: whether a signing secret is
/// configured, and when the last delivery was received (null = never seen).
/// A webhook is inbound, so there is no active "test" — these two facts are all
/// the server can truthfully report.
/// </summary>
public sealed record WebhookStatus(
    string Platform, bool SecretConfigured, DateTimeOffset? LastReceivedUtc);
