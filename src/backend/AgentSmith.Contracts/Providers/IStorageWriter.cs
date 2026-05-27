using AgentSmith.Domain.Entities;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Writes content to a storage location (disk, SharePoint, Azure Blob).
/// </summary>
public interface IStorageWriter : ITypedProvider
{
    Task<AttachmentRef> WriteAsync(
        string fileName,
        Stream content,
        string mimeType,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        AttachmentRef attachmentRef,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
