namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// p0317: shared file-extension → MIME-type guess for ticket attachments.
/// Covers the supported image types plus the text-like document types;
/// everything else falls back to the caller-supplied default.
/// </summary>
internal static class AttachmentMimeTypes
{
    public static string Guess(string fileName, string fallback = "application/octet-stream") =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".txt" => "text/plain",
            ".md" => "text/markdown",
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => fallback
        };
}
