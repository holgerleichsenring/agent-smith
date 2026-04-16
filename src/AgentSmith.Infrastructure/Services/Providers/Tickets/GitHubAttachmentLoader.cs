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

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp"
    };

    /// <summary>
    /// Parses markdown body for image URLs.
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
            var mimeType = GuessMimeType(fileName);
            refs.Add(new AttachmentRef(url, fileName, mimeType));
        }

        return refs;
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

    private static string GuessMimeType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "image/png" // Default for githubusercontent URLs without extension
        };
    }
}
