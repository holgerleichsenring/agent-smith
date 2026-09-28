using System.Text.Json;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// The initial run context for a run a pull request started — its number, head, base,
/// author, the head branch to check out, and the configured repo it belongs to. Each host's
/// payload is read by its own method; the context they produce is built in one place, so a
/// review, a label-triggered scan and a chat-requested scan of the same pull request see the
/// same code.
/// </summary>
public sealed class PrRunContextFactory
{
    public Dictionary<string, object> FromGitHub(JsonElement pullRequest, string repoName) =>
        Build(new PullRequestFacts(
            Number: pullRequest.GetProperty("number").GetInt32().ToString(),
            HeadSha: Text(pullRequest, "head", "sha"),
            BaseSha: Text(pullRequest, "base", "sha"),
            Author: Text(pullRequest, "user", "login"),
            HeadBranch: Text(pullRequest, "head", "ref")), repoName);

    /// <summary>The GitLab merge-request hook carries no base sha; AnalyzePrDiff publishes
    /// the authoritative head/base pair from the platform API.</summary>
    public Dictionary<string, object> FromGitLab(JsonElement root, string repoName)
    {
        var attrs = root.GetProperty("object_attributes");
        return Build(new PullRequestFacts(
            Number: attrs.GetProperty("iid").GetInt32().ToString(),
            HeadSha: Text(attrs, "last_commit", "id"),
            BaseSha: null,
            Author: Text(root, "user", "username"),
            HeadBranch: Text(attrs, "source_branch")), repoName);
    }

    /// <summary>A pull request known only by its number — a chat command names nothing else —
    /// read back from the host through its diff provider.</summary>
    public Dictionary<string, object> FromDiff(string prNumber, PrDiff diff, string repoName) =>
        Build(new PullRequestFacts(
            Number: prNumber, HeadSha: diff.HeadSha, BaseSha: diff.BaseSha,
            Author: diff.Author, HeadBranch: diff.HeadBranch), repoName);

    private static Dictionary<string, object> Build(PullRequestFacts pr, string repoName)
    {
        var context = new Dictionary<string, object>
        {
            [ContextKeys.PrNumber] = pr.Number,
            [ContextKeys.PrAuthor] = pr.Author ?? "",
            [ContextKeys.SourceOverrideRepo] = repoName,
        };
        if (!string.IsNullOrEmpty(pr.HeadBranch)) context[ContextKeys.CheckoutBranch] = pr.HeadBranch;
        if (!string.IsNullOrEmpty(pr.HeadSha)) context[ContextKeys.PrHead] = pr.HeadSha;
        if (!string.IsNullOrEmpty(pr.BaseSha)) context[ContextKeys.PrBase] = pr.BaseSha;
        return context;
    }

    private static string? Text(JsonElement element, params string[] path)
    {
        foreach (var segment in path)
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(segment, out element))
                return null;
        return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    }
}
