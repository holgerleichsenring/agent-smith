using System.Net;
using System.Text.RegularExpressions;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// 2026-10-25-9dbbd: the GitHub uploads in an issue's rendered body (<c>body_html</c>). An image
/// upload renders as an <c>&lt;img src&gt;</c> on private-user-images.githubusercontent.com, a URL
/// signed with its own short-lived JWT, so it is downloaded without any credential; a file upload
/// renders as an <c>&lt;a href&gt;</c> to github.com/…/files/…. camo.githubusercontent.com proxies an
/// EXTERNAL image and is skipped, as is every other host. Each URL is listed once.
/// </summary>
public sealed class GitHubIssueHtmlRefs
{
    private static readonly Regex ImageSource = new(
        @"<img\b[^>]*?\ssrc\s*=\s*""(?<url>[^""]+)""", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex LinkTarget = new(
        @"<a\b[^>]*?\shref\s*=\s*""(?<url>[^""]+)""", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly HashSet<string> ImageHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "private-user-images.githubusercontent.com",
        "user-images.githubusercontent.com",
    };

    public IReadOnlyList<AttachmentRef> Parse(string bodyHtml)
    {
        ArgumentNullException.ThrowIfNull(bodyHtml);
        var images = Urls(ImageSource, bodyHtml).Where(u => ImageHosts.Contains(u.Host)).Select(Image);
        var files = Urls(LinkTarget, bodyHtml).Where(IsFileUpload).Select(File);
        return [.. images.Concat(files).DistinctBy(r => r.Uri, StringComparer.Ordinal)];
    }

    private static IEnumerable<Uri> Urls(Regex pattern, string html) =>
        pattern.Matches(html)
            .Select(m => WebUtility.HtmlDecode(m.Groups["url"].Value))
            .Select(u => Uri.TryCreate(u, UriKind.Absolute, out var uri) ? uri : null)
            .OfType<Uri>()
            .Where(u => u.Scheme == Uri.UriSchemeHttps);

    private static bool IsFileUpload(Uri uri) =>
        uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
        && uri.AbsolutePath.Contains("/files/", StringComparison.OrdinalIgnoreCase);

    private static AttachmentRef Image(Uri uri)
    {
        var fileName = Path.GetFileName(uri.AbsolutePath);
        return new AttachmentRef(uri.AbsoluteUri, fileName, AttachmentMimeTypes.Guess(fileName, fallback: "image/png"));
    }

    private static AttachmentRef File(Uri uri)
    {
        var fileName = Path.GetFileName(uri.AbsolutePath);
        return new AttachmentRef(uri.AbsoluteUri, fileName, AttachmentMimeTypes.Guess(fileName));
    }
}
