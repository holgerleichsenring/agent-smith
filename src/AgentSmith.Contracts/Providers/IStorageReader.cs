using AgentSmith.Domain.Entities;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Reads content from a storage location. Each reader decides which
/// AttachmentRefs it can handle via CanHandle (scheme, host, path pattern).
/// </summary>
public interface IStorageReader
{
    bool CanHandle(AttachmentRef attachmentRef);

    Task<Stream> ReadAsync(
        AttachmentRef attachmentRef,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        AttachmentRef attachmentRef,
        CancellationToken cancellationToken = default);
}
