using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Webhooks;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Webhooks;

/// <summary>
/// Posts reply comments on Azure DevOps Pull Requests via the REST API threads endpoint.
/// </summary>
public sealed class AzureDevOpsPrCommentReplyService(
    SecretsProvider secrets,
    IHttpClientFactory httpClientFactory,
    ILogger<AzureDevOpsPrCommentReplyService> logger) : IPrCommentReplyService
{
    public async Task ReplyAsync(CommentIntent originalComment, string text, CancellationToken cancellationToken)
    {
        var token = secrets.GetRequired("AZDO_TOKEN");
        var orgUrl = secrets.GetRequired("AZDO_ORG_URL").TrimEnd('/');

        var parts = originalComment.RepoFullName.Split('/');
        if (parts.Length != 2)
            throw new ArgumentException(
                $"Invalid repository full name: '{originalComment.RepoFullName}'. Expected 'project/repo'.");

        var project = parts[0];
        var repo = parts[1];

        var url = $"{orgUrl}/{project}/_apis/git/repositories/{repo}" +
                  $"/pullRequests/{originalComment.PrIdentifier}/threads?api-version=7.1";

        var body = new
        {
            comments = new[]
            {
                new { parentCommentId = 0, content = text, commentType = 1 }
            },
            status = 1 // Active
        };

        var client = httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{token}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Content = new StringContent(
            JsonSerializer.Serialize(body),
            Encoding.UTF8,
            "application/json");

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        logger.LogInformation(
            "Posted reply on {Repo}#{Pr} (original comment {CommentId})",
            originalComment.RepoFullName, originalComment.PrIdentifier, originalComment.CommentId);
    }
}
