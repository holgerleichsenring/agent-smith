using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// Extracts image attachments from GitHub issue bodies by parsing markdown image syntax.
/// GitHub uploads images to user-images.githubusercontent.com — URLs are publicly accessible.
/// </summary>
public sealed class GitHubAttachmentLoader(
    HttpClient httpClient,
    ILogger logger) : IAttachmentLoader
{
    // Matches ![alt](url) markdown image syntax
    private static readonly Regex MarkdownImagePattern = new(
        @"!\[[^\]]*\]\((?<url>https?://[^)\s]+)\)",
        RegexOptions.Compiled);

    // p0317: matches [name](url) file links (no leading !) — GitHub renders
    // non-image uploads (pdf/docx/…) as plain markdown links to
    // github.com/user-attachments/files/… (or the older …/{repo}/files/…).
    private static readonly Regex MarkdownFileLinkPattern = new(
        @"(?<!\!)\[[^\]]*\]\((?<url>https?://[^)\s]+)\)",
        RegexOptions.Compiled);

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp"
    };

    /// <summary>
    /// Parses the markdown body for attachment URLs — image embeds plus
    /// GitHub-hosted file uploads (p0317). External links are never treated
    /// as attachments: only GitHub upload hosts are downloaded.
    /// </summary>
    public static IReadOnlyList<AttachmentRef> ParseRefs(string? issueBody)
    {
        if (string.IsNullOrWhiteSpace(issueBody))
            return [];

        var refs = new List<AttachmentRef>();
        foreach (Match match in MarkdownImagePattern.Matches(issueBody))
        {
            var url = match.Groups["url"].Value;
            if (!IsImageUrl(url)) continue;

            var fileName = Path.GetFileName(new Uri(url).AbsolutePath);
            var mimeType = AttachmentMimeTypes.Guess(
                fileName, fallback: "image/png"); // githubusercontent URLs often lack an extension
            refs.Add(new AttachmentRef(url, fileName, mimeType));
        }

        foreach (Match match in MarkdownFileLinkPattern.Matches(issueBody))
        {
            var url = match.Groups["url"].Value;
            if (!IsGitHubFileUpload(url)) continue;

            var fileName = Path.GetFileName(new Uri(url).AbsolutePath);
            refs.Add(new AttachmentRef(url, fileName, AttachmentMimeTypes.Guess(fileName)));
        }

        return refs;
    }

    private static bool IsGitHubFileUpload(string url)
    {
        try
        {
            var uri = new Uri(url);
            return uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
                && uri.AbsolutePath.Contains("/files/", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public async Task<byte[]?> DownloadAsync(
        AttachmentRef attachment, CancellationToken cancellationToken)
    {
        try
        {
            var content = await httpClient.GetByteArrayAsync(attachment.Uri, cancellationToken);
            if (content.Length > TicketImageAttachment.MaxSizeBytes)
            {
                logger.LogWarning("GitHub image {File} exceeds 5MB limit, skipping", attachment.FileName);
                return null;
            }
            return content;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error downloading GitHub image {Url}", attachment.Uri);
            return null;
        }
    }

    private static bool IsImageUrl(string url)
    {
        try
        {
            var path = new Uri(url).AbsolutePath;
            return ImageExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                   || url.Contains("githubusercontent.com", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

}
