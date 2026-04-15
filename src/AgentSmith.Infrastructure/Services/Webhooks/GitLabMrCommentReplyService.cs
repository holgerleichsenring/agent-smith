using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Webhooks;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Webhooks;

/// <summary>
/// Posts reply comments on GitLab Merge Requests via the REST API v4 notes endpoint.
/// </summary>
public sealed class GitLabMrCommentReplyService(
    SecretsProvider secrets,
    IHttpClientFactory httpClientFactory,
    ILogger<GitLabMrCommentReplyService> logger) : IPrCommentReplyService
{
    public async Task ReplyAsync(CommentIntent originalComment, string text, CancellationToken cancellationToken)
    {
        var token = secrets.GetRequired("GITLAB_TOKEN");
        var baseUrl = Environment.GetEnvironmentVariable("GITLAB_URL")?.TrimEnd('/') ?? "https://gitlab.com";

        var encodedPath = Uri.EscapeDataString(originalComment.RepoFullName);
        var url = $"{baseUrl}/api/v4/projects/{encodedPath}/merge_requests/{originalComment.PrIdentifier}/notes";

        var client = httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("PRIVATE-TOKEN", token);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { body = text }),
            Encoding.UTF8,
            "application/json");

        using var response = await client.SendAsync(request, cancellationToken);
        await response.EnsureSuccessWithBodyAsync(cancellationToken);

        logger.LogInformation(
            "Posted reply on {Repo}!{Mr} (original comment {CommentId})",
            originalComment.RepoFullName, originalComment.PrIdentifier, originalComment.CommentId);
    }
}
