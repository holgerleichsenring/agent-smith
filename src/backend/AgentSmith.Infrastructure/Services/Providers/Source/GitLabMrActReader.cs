using System.Text.Json;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Webhooks;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-08-f147: GitLab's standing requests for changes, from the system notes GitLab writes
/// itself — "requested changes" for every submission, "approved this merge request" for every
/// approval. Per author the newer of the two decides, as GitHub's latest review does. Neither the
/// merge request's author nor the token's own user counts; the act's time is the note's.
/// </summary>
public sealed class GitLabMrActReader(
    string baseUrl, string projectPath, string token, string repoUrl, HttpClient http, ILogger logger) : IPrReviewActReader
{
    private readonly GitLabNotesPager _notes = new(baseUrl, projectPath, token, http, logger);
    private readonly GitLabUsers _users = new(baseUrl, token, http);

    public const string RequestedChanges = "requested changes";
    public const string Approved = "approved this merge request";
    private const int MaxPages = 10;

    public async Task<IReadOnlyList<PrReviewNote>> ChangesRequestedAsync(string prUrl, CancellationToken cancellationToken)
    {
        if (!GitLabMergeRequestUpdater.TryParseMergeRequestIid(prUrl, out var iid))
            throw new ArgumentException($"Not a GitLab merge request URL: {prUrl}", nameof(prUrl));
        var excluded = new[] { await MrAuthorAsync(iid, cancellationToken), await _users.TokenUserIdAsync(cancellationToken) };
        var standing = (await _notes.ReadAsync(iid.ToString(), MaxPages, cancellationToken))
            .Where(n => n.System && n.AuthorId is not null && n.Body is RequestedChanges or Approved)
            .GroupBy(n => n.AuthorId!.Value)
            .Select(g => g.MaxBy(n => n.CreatedAt)!)
            .Where(n => n.Body == RequestedChanges && !excluded.Contains(n.AuthorId))
            .ToList();
        var acts = new List<PrReviewNote>();
        foreach (var note in standing) acts.Add(await ActAsync(note, cancellationToken));
        return acts;
    }

    private async Task<PrReviewNote> ActAsync(GitLabNote note, CancellationToken ct)
    {
        var id = note.AuthorId!.Value;
        return new PrReviewNote(
            new PrCommentAuthor(repoUrl, Uri.UnescapeDataString(projectPath), id.ToString(), note.AuthorUsername ?? id.ToString()),
            await _users.IsBotAsync(id, ct), false, note.CreatedAt, note.Body);
    }

    private async Task<long?> MrAuthorAsync(int iid, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/v4/projects/{projectPath}/merge_requests/{iid}");
        request.Headers.Add("PRIVATE-TOKEN", token);
        using var response = await http.SendAsync(request, ct);
        await response.EnsureSuccessWithBodyAsync(ct);
        using var mr = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return mr.RootElement.TryGetProperty("author", out var a) && a.ValueKind == JsonValueKind.Object
            && a.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number ? id.GetInt64() : null;
    }
}
