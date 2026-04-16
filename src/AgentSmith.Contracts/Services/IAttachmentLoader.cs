using AgentSmith.Domain.Entities;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Loads image attachment references from a ticket provider and downloads their content.
/// Each platform (Jira, GitHub, GitLab, Azure DevOps) has its own implementation.
/// </summary>
public interface IAttachmentLoader
{
    /// <summary>
    /// Downloads an attachment's binary content.
    /// Returns null if the download fails or the attachment exceeds the size limit.
    /// </summary>
    Task<byte[]?> DownloadAsync(AttachmentRef attachment, CancellationToken cancellationToken);
}
