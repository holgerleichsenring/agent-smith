namespace AgentSmith.Domain.Entities;

/// <summary>
/// A reference to an attachment — URI + type hint.
/// The IStorageReader resolves the actual content.
/// </summary>
public sealed record AttachmentRef(
    string Uri,
    string FileName,
    string MimeType,
    long? SizeBytes = null);
