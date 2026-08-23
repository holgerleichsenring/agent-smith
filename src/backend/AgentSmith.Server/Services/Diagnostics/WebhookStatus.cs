namespace AgentSmith.Server.Services.Diagnostics;

/// <summary>
/// The honest webhook signal per platform: whether a signing secret is
/// configured, and when the last delivery was received (null = never seen).
/// A webhook is inbound, so there is no active "test" — these two facts are all
/// the server can truthfully report.
/// </summary>
public sealed record WebhookStatus(
    string Platform, bool SecretConfigured, DateTimeOffset? LastReceivedUtc)
{
    /// <summary>
    /// p0506: the CONJUNCTION neither fact states on its own — a delivery reached this
    /// deployment and nothing verified that the platform sent it. It rides on a delivery
    /// that HAPPENED rather than on a trigger that exists, because every polling-only
    /// project carries a synthesized webhook trigger in the model; a deployment that
    /// exposes no webhook never records one, so it never claims to be open.
    /// </summary>
    public bool AcceptedUnsignedDelivery => !SecretConfigured && LastReceivedUtc is not null;
}
