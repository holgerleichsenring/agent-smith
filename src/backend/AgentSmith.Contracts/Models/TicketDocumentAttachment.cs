using AgentSmith.Domain.Entities;

namespace AgentSmith.Contracts.Models;

/// <summary>
/// p0317: a downloaded text-like ticket document (txt/md/pdf/docx) — the
/// non-image sibling of <see cref="TicketImageAttachment"/>. Materialized into
/// the run-record attachments/ dir at AgenticMaster time (pdf/docx via
/// markitdown) so the master can read it; other binary attachments are never
/// downloaded, only listed by name + size.
/// </summary>
public sealed record TicketDocumentAttachment(
    AttachmentRef Ref,
    byte[] Content)
{
    /// <summary>Maximum document size in bytes (5 MB, same cap as images).</summary>
    public const long MaxSizeBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> PlainTextExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".txt", ".md" };

    private static readonly HashSet<string> ConvertibleExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".docx" };

    public string FileName => Ref.FileName;

    /// <summary>True for txt/md — written as-is, no conversion needed.</summary>
    public bool IsPlainText => PlainTextExtensions.Contains(Extension(Ref.FileName));

    public static bool IsTextLikeDocument(AttachmentRef attachment)
    {
        var ext = Extension(attachment.FileName);
        return (PlainTextExtensions.Contains(ext) || ConvertibleExtensions.Contains(ext))
            && (attachment.SizeBytes is null || attachment.SizeBytes <= MaxSizeBytes);
    }

    private static string Extension(string fileName) => Path.GetExtension(fileName);
}
