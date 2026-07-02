namespace AgentSmith.Contracts.Services;

/// <summary>
/// Records and reads the timestamp of the last webhook delivery received per
/// platform (github / gitlab / jira / azuredevops). A webhook is inbound, so it
/// cannot be actively probed — this "last seen" signal is the honest substitute:
/// it tells the operator whether deliveries are actually arriving.
/// </summary>
public interface IWebhookDeliveryTracker
{
    Task RecordAsync(string platform, DateTimeOffset receivedAtUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, DateTimeOffset>> GetLastSeenAsync(CancellationToken cancellationToken = default);
}
