using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// 2026-10-08-f147: a GitLab merge-request update that shows a reviewer in requested_changes. A
/// first request changes the reviewer list; a repeated one changes nothing, so the delivery is taken
/// when it carries no oldrev (a push) and no change besides the reviewers. The act and its time come
/// from GitLab's own "requested changes" note, read from the host — the newest of a reviewer this
/// delivery still shows in requested_changes. Registered after the MR event and label handlers.
/// </summary>
public sealed class GitLabMrReviewWebhookHandler(
    PrReworkAdmission admission,
    IConfiguredRepoFinder repoFinder,
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    ISourceProviderFactory sources,
    ILogger<GitLabMrReviewWebhookHandler> logger) : IWebhookHandler
{
    public bool CanHandle(string platform, string eventType) =>
        platform == "gitlab" && eventType == "merge_request";

    public async Task<WebhookResult> HandleAsync(
        string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            if (!GitLabReviewDelivery.IsReviewSubmission(root, out var requesting))
                return WebhookResult.NotHandled("not a submitted request for changes");
            var mr = root.GetProperty("object_attributes");
            var repoUrl = PayloadActTime.Text(root, "project", "web_url") ?? string.Empty;
            if (await StandingActAsync(repoUrl, PayloadActTime.Text(mr, "url") ?? string.Empty, requesting, cancellationToken) is not { } act)
                return WebhookResult.NotHandled("no standing request for changes on the merge request");
            return await admission.AdmitAsync(Request(root, mr, repoUrl, act), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read the GitLab merge-request review delivery");
            return WebhookResult.NotHandled();
        }
    }

    private async Task<PrReviewNote?> StandingActAsync(
        string repoUrl, string mrUrl, IReadOnlySet<string> requesting, CancellationToken ct)
    {
        var repo = repoFinder.FindAll(configLoader.LoadConfig(serverContext.ConfigPath), repoUrl).FirstOrDefault();
        if (repo is null || sources.Create(repo.Repo) is not IPrReviewActReader reader) return null;
        var acts = await reader.ChangesRequestedAsync(mrUrl, ct);
        return acts.Where(a => a.Author is not null && requesting.Contains(a.Author.AuthorId)).MaxBy(a => a.At);
    }

    private static PrReviewRequest Request(JsonElement root, JsonElement mr, string repoUrl, PrReviewNote act) =>
        new(RepoType.GitLab, repoUrl, PayloadActTime.Text(mr, "source_branch"),
            Number(mr, "source_project_id") == Number(mr, "target_project_id"), act.Author!,
            Number(mr, "author_id"), Number(mr, "iid") ?? string.Empty,
            new ReworkAct(act.Author!.AuthorLogin, act.At, ReworkChannel.PullRequest));

    private static string? Number(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64().ToString() : null;
}
