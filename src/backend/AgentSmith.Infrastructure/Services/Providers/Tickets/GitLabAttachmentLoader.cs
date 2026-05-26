using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// Extracts image attachments from GitLab issue bodies by parsing markdown image syntax.
/// Resolves relative /uploads/ paths to absolute URLs and downloads with Private-Token.
/// </summary>
public sealed class GitLabAttachmentLoader(
    string baseUrl,
    string projectPath,
    string privateToken,
    HttpClient httpClient,
    ILogger logger) : IAttachmentLoader
{
    private static readonly Regex MarkdownImagePattern = new(
        @"!\[[^\]]*\]\((?<url>[^)\s]+)\)",
        RegexOptions.Compiled);

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp"
    };

    /// <summary>
    /// Parses markdown body for image URLs, resolving relative upload paths.
    /// </summary>
    public IReadOnlyList<AttachmentRef> ParseRefs(string? issueBody)
    {
        if (string.IsNullOrWhiteSpace(issueBody))
            return [];

        var refs = new List<AttachmentRef>();
        foreach (Match match in MarkdownImagePattern.Matches(issueBody))
        {
            var url = match.Groups["url"].Value;

            // Resolve relative GitLab upload URLs
            if (url.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
                url = $"{baseUrl.TrimEnd('/')}/{projectPath}{url}";

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
            using var request = new HttpRequestMessage(HttpMethod.Get, attachment.Uri);
            request.Headers.Add("PRIVATE-TOKEN", privateToken);

            var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to download GitLab image {File}: {Status}",
                    attachment.FileName, response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (content.Length > TicketImageAttachment.MaxSizeBytes)
            {
                logger.LogWarning("GitLab image {File} exceeds 5MB limit, skipping", attachment.FileName);
                return null;
            }
            return content;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error downloading GitLab image {Url}", attachment.Uri);
            return null;
        }
    }

    private static bool IsImageUrl(string url)
    {
        try
        {
            var path = new Uri(url).AbsolutePath;
            return ImageExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
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
            _ => "image/png"
        };
    }
}
