using System.Globalization;
using System.Text.Json;
using AgentSmith.Contracts.Sweep;
using AgentSmith.Contracts.Webhooks;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-08-10b0: a GitLab project's open merge requests, most recently updated first, and the
/// notes of those updated since the time (less a minute — GitLab touches updated_at at most once a
/// minute). System notes are no comments. The author carries the project and user ids the member
/// lookup takes.
/// </summary>
public sealed class GitLabOpenPullRequests(string baseUrl, string projectPath, string token, HttpClient http) : IOpenPullRequestLister
{
    private const int PageSize = 100;

    public bool ReadsCommentsPerPullRequest => false;

    public async Task<OpenPullRequestsPage> ListOpenAsync(string? resume, int maxPages, CancellationToken ct)
    {
        var start = int.TryParse(resume, out var p) ? p : 1;
        var items = new List<OpenPullRequest>();
        for (var page = start; page < start + maxPages; page++)
        {
            using var doc = await GetAsync($"merge_requests?state=opened&order_by=updated_at&sort=desc&per_page={PageSize}&page={page}", ct);
            items.AddRange(doc.RootElement.EnumerateArray().Select(Pr));
            if (doc.RootElement.GetArrayLength() < PageSize) return new OpenPullRequestsPage(items);
        }
        return new OpenPullRequestsPage(items, true, (start + maxPages).ToString());
    }

    public async Task<IReadOnlyList<PrSweepComment>> CommentsSinceAsync(
        IReadOnlyList<OpenPullRequest> pullRequests, DateTimeOffset since, CancellationToken ct)
    {
        var comments = new List<PrSweepComment>();
        foreach (var pr in pullRequests.Where(p => p.UpdatedAt > since - TimeSpan.FromSeconds(60)))
        {
            using var doc = await GetAsync($"merge_requests/{pr.Number}/notes?sort=asc&order_by=created_at&per_page={PageSize}", ct);
            comments.AddRange(doc.RootElement.EnumerateArray().Where(n => !(n.TryGetProperty("system", out var s) && s.ValueKind == JsonValueKind.True))
                .Select(n => Note(pr, n)).Where(c => c.CreatedAt > since));
        }
        return comments;
    }

    private static OpenPullRequest Pr(JsonElement mr) => new(
        mr.GetProperty("iid").GetInt64().ToString(), Str(mr, "web_url") ?? string.Empty, Str(mr, "sha"),
        mr.TryGetProperty("diff_refs", out var refs) && refs.ValueKind == JsonValueKind.Object ? Str(refs, "base_sha") : null,
        Str(mr, "source_branch"), mr.TryGetProperty("author", out var a) ? Str(a, "username") : null,
        mr.TryGetProperty("labels", out var l) && l.ValueKind == JsonValueKind.Array ? [.. l.EnumerateArray().Select(x => x.GetString() ?? "")] : [],
        Time(mr, "created_at"), Time(mr, "updated_at"));

    private static PrSweepComment Note(OpenPullRequest pr, JsonElement note)
    {
        var author = note.GetProperty("author");
        var project = note.TryGetProperty("project_id", out var pid) && pid.ValueKind == JsonValueKind.Number ? pid.GetInt64().ToString() : string.Empty;
        var repoUrl = pr.Url.Split("/-/merge_requests/")[0];
        return new PrSweepComment(pr.Number, note.GetProperty("id").GetInt64().ToString("D20"), Time(note, "created_at"), Str(note, "body") ?? string.Empty,
            new PrCommentAuthor(repoUrl, project, author.GetProperty("id").GetInt64().ToString(), Str(author, "username") ?? string.Empty));
    }

    private async Task<JsonDocument> GetAsync(string path, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/v4/projects/{projectPath}/{path}");
        request.Headers.Add("PRIVATE-TOKEN", token);
        using var response = await http.SendAsync(request, ct);
        await response.EnsureSuccessWithBodyAsync(ct);
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
    }

    private static DateTimeOffset Time(JsonElement e, string name) =>
        Str(e, name) is { } at ? DateTimeOffset.Parse(at, CultureInfo.InvariantCulture) : DateTimeOffset.MinValue;

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
