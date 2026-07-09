using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// Extracts image attachments from GitLab issue bodies by parsing markdown image syntax.
/// Resolves relative /uploads/ paths to absolute URLs and downloads with Private-Token.
/// </summary>
public sealed class GitLabAttachmentLoader(
    GitLabTicketConnection connection,
    HttpClient httpClient,
    ILogger logger) : IAttachmentLoader
{
    private readonly string _baseUrl = connection.BaseUrl.TrimEnd('/');
    private readonly string _projectPath = connection.ProjectPath;
    private readonly string _privateToken = connection.PrivateToken;

    private static readonly Regex MarkdownImagePattern = new(
        @"!\[[^\]]*\]\((?<url>[^)\s]+)\)",
        RegexOptions.Compiled);

    // p0317: matches [name](url) file links (no leading !) — GitLab renders
    // non-image uploads (pdf/docx/…) as plain markdown links to /uploads/….
    private static readonly Regex MarkdownFileLinkPattern = new(
        @"(?<!\!)\[[^\]]*\]\((?<url>[^)\s]+)\)",
        RegexOptions.Compiled);

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp"
    };

    /// <summary>
    /// Parses the markdown body for attachment URLs, resolving relative upload
    /// paths — image embeds plus project-hosted file uploads (p0317). External
    /// links are never treated as attachments: only /uploads/ is downloaded.
    /// </summary>
    public IReadOnlyList<AttachmentRef> ParseRefs(string? issueBody)
    {
        if (string.IsNullOrWhiteSpace(issueBody))
            return [];

        var refs = new List<AttachmentRef>();
        foreach (Match match in MarkdownImagePattern.Matches(issueBody))
        {
            var url = ResolveUploadUrl(match.Groups["url"].Value);
            if (!IsImageUrl(url)) continue;

            var fileName = Path.GetFileName(new Uri(url).AbsolutePath);
            refs.Add(new AttachmentRef(url, fileName, AttachmentMimeTypes.Guess(fileName, "image/png")));
        }

        foreach (Match match in MarkdownFileLinkPattern.Matches(issueBody))
        {
            var raw = match.Groups["url"].Value;
            if (!raw.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase)) continue;

            var url = ResolveUploadUrl(raw);
            var fileName = Path.GetFileName(new Uri(url).AbsolutePath);
            refs.Add(new AttachmentRef(url, fileName, AttachmentMimeTypes.Guess(fileName)));
        }

        return refs;
    }

    private string ResolveUploadUrl(string url) =>
        url.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase)
            ? $"{_baseUrl}/{_projectPath}{url}"
            : url;

    public async Task<byte[]?> DownloadAsync(
        AttachmentRef attachment, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, attachment.Uri);
            request.Headers.Add("PRIVATE-TOKEN", _privateToken);

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

}
