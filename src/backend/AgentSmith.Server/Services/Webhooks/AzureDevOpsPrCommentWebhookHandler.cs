using System.Text.Json;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// Handles Azure DevOps Pull Request comment events. This handler owns the Azure DevOps
/// payload shape; what the comment starts is decided by <see cref="PrCommentCommandAdmission"/>
/// with the repository's Git Contribute permission as the author trust.
/// </summary>
public sealed class AzureDevOpsPrCommentWebhookHandler(
    PrCommentCommandAdmission admission,
    [FromKeyedServices("azuredevops")] IPrCommentAuthorTrust authorTrust,
    ILogger<AzureDevOpsPrCommentWebhookHandler> logger) : IWebhookHandler
{
    public bool CanHandle(string platform, string eventType) =>
        platform == "azuredevops"
        && eventType.Equals("ms.vss-code.git-pullrequest-comment-event", StringComparison.OrdinalIgnoreCase);

    public async Task<WebhookResult> HandleAsync(
        string payload, IDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var resource = doc.RootElement.GetProperty("resource");
            return await admission.AdmitAsync(ReadCommand(resource), authorTrust, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse Azure DevOps PR comment webhook");
            return WebhookResult.NotHandled();
        }
    }

    private static PrCommentCommand ReadCommand(JsonElement resource)
    {
        var comment = resource.GetProperty("comment");
        var authorElement = comment.GetProperty("author");
        var pullRequest = resource.GetProperty("pullRequest");
        var prId = pullRequest.GetProperty("pullRequestId").GetInt32();
        var repo = pullRequest.GetProperty("repository");
        var project = repo.GetProperty("project");
        var repoFullName = $"{project.GetProperty("name").GetString()}/{repo.GetProperty("name").GetString()}";
        var author = new PrCommentAuthor(
            RepositoryUrl: repo.GetProperty("remoteUrl").GetString() ?? "",
            RepositoryId: repo.GetProperty("id").GetString() ?? "",
            AuthorId: authorElement.GetProperty("id").GetString() ?? "",
            AuthorLogin: authorElement.GetProperty("uniqueName").GetString() ?? "")
        {
            ProjectId = project.GetProperty("id").GetString(),
        };
        return new PrCommentCommand(
            comment.GetProperty("content").GetString() ?? "", author,
            $"{repoFullName}#{prId}", $"pr:{repoFullName}#{prId}", prId.ToString());
    }
}
