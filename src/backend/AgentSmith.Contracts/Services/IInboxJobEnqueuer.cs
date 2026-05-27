namespace AgentSmith.Contracts.Services;

/// <summary>
/// Enqueues document analysis pipeline jobs. Implemented by the host/dispatcher.
/// </summary>
public interface IInboxJobEnqueuer
{
    Task EnqueueAsync(string filePath, string? metadata, CancellationToken cancellationToken);
}
