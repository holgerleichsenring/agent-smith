using System.Text.Json;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// Handles GitLab Note Hook events on Merge Requests. This handler owns the GitLab payload
/// shape; what the comment starts is decided by <see cref="PrCommentCommandAdmission"/>
/// with GitLab's member-access trust.
/// </summary>
public sealed class GitLabMrCommentWebhookHandler(
    PrCommentCommandAdmission admission,
    [FromKeyedServices("gitlab")] IPrCommentAuthorTrust authorTrust,
    ILogger<GitLabMrCommentWebhookHandler> logger) : IWebhookHandler
{
    public bool CanHandle(string platform, string eventType) =>
        platform == "gitlab" && eventType == "note hook";

    public async Task<WebhookResult> HandleAsync(
        string payload, IDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            var attrs = root.GetProperty("object_attributes");

            var noteableType = attrs.GetProperty("noteable_type").GetString() ?? "";
            if (!noteableType.Equals("MergeRequest", StringComparison.OrdinalIgnoreCase))
                return WebhookResult.NotHandled();

            return await admission.AdmitAsync(ReadCommand(root, attrs), authorTrust, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse GitLab MR comment webhook");
            return WebhookResult.NotHandled();
        }
    }

    private static PrCommentCommand ReadCommand(JsonElement root, JsonElement attrs)
    {
        var project = root.GetProperty("project");
        var user = root.GetProperty("user");
        var repoFullName = project.GetProperty("path_with_namespace").GetString() ?? "";
        var mrIid = root.GetProperty("merge_request").GetProperty("iid").GetInt32();
        var author = new PrCommentAuthor(
            RepositoryUrl: project.GetProperty("web_url").GetString() ?? "",
            RepositoryId: project.GetProperty("id").GetInt64().ToString(),
            AuthorId: user.GetProperty("id").GetInt64().ToString(),
            AuthorLogin: user.GetProperty("username").GetString() ?? "");
        return new PrCommentCommand(
            attrs.GetProperty("note").GetString() ?? "", author,
            $"{repoFullName}!{mrIid}", $"mr:{repoFullName}!{mrIid}", mrIid.ToString());
    }
}
