using AgentSmith.Domain.Entities;

namespace AgentSmith.Contracts.Models;

/// <summary>
/// A downloaded image attachment ready for LLM vision input.
/// </summary>
public sealed record TicketImageAttachment(
    AttachmentRef Ref,
    byte[] Content)
{
    /// <summary>
    /// Base64-encoded content for embedding in LLM API requests.
    /// </summary>
    public string Base64 => Convert.ToBase64String(Content);

    /// <summary>
    /// Media type for the LLM content block (e.g. "image/png").
    /// </summary>
    public string MediaType => Ref.MimeType;

    private static readonly HashSet<string> SupportedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/gif", "image/webp"
    };

    /// <summary>
    /// Maximum image size in bytes (5 MB).
    /// </summary>
    public const long MaxSizeBytes = 5 * 1024 * 1024;

    public static bool IsSupportedImage(string mimeType) =>
        SupportedMimeTypes.Contains(mimeType);

    public static bool IsSupportedImage(AttachmentRef attachment) =>
        IsSupportedImage(attachment.MimeType)
        && (attachment.SizeBytes is null || attachment.SizeBytes <= MaxSizeBytes);
}
