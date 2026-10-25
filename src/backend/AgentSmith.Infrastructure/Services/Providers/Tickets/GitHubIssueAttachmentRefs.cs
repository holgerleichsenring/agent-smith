using System.Text.Json;
using AgentSmith.Domain.Entities;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// 2026-10-25-9dbbd: an issue's attachment refs, read through the raw Octokit connection with the
/// full media type, which carries the markdown body and the rendered <c>body_html</c> in one read.
/// The rendered body names private-repository uploads by their signed URLs
/// (<see cref="GitHubIssueHtmlRefs"/>); with no rendered body the markdown is parsed as before.
/// </summary>
public sealed class GitHubIssueAttachmentRefs(IGitHubClient client, string owner, string repo)
{
    private const string FullMediaType = "application/vnd.github.full+json";
    private readonly GitHubIssueHtmlRefs _html = new();

    public async Task<IReadOnlyList<AttachmentRef>> ReadAsync(int number, CancellationToken cancellationToken)
    {
        var response = await client.Connection.Get<string>(
            new Uri($"repos/{owner}/{repo}/issues/{number}", UriKind.Relative), null, FullMediaType, cancellationToken);
        // Octokit deserializes a JSON reply into T, which leaves a string Body empty; the raw text is the HTTP body.
        using var doc = JsonDocument.Parse(response.HttpResponse.Body as string ?? "{}");
        return Text(doc.RootElement, "body_html") is { Length: > 0 } html
            ? _html.Parse(html)
            : GitHubAttachmentLoader.ParseRefs(Text(doc.RootElement, "body"));
    }

    private static string? Text(JsonElement issue, string name) =>
        issue.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
