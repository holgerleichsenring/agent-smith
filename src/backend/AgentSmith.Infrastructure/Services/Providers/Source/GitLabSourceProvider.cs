using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;
using Repository = AgentSmith.Domain.Entities.Repository;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// Source provider for GitLab repositories. CheckoutAsync is metadata-only —
/// the actual git clone happens sandbox-side via Step{Kind=Run, Command=git, ...}.
/// Default-branch resolution stays here (REST API call, not git plumbing).
/// </summary>
public sealed class GitLabSourceProvider : ISourceProvider, IPrCommentProvider
{
    private readonly string _baseUrl;
    private readonly string _projectPath;
    private readonly string _cloneUrl;
    private readonly string _privateToken;
    private readonly string? _configuredDefaultBranch;
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitLabSourceProvider> _logger;
    private string? _cachedDefaultBranch;

    public string ProviderType => "GitLab";

    public GitLabSourceProvider(
        GitLabSourceConnection connection,
        HttpClient httpClient,
        ILogger<GitLabSourceProvider> logger)
    {
        _baseUrl = connection.BaseUrl.TrimEnd('/');
        _projectPath = connection.ProjectPath;
        _cloneUrl = connection.CloneUrl;
        _privateToken = connection.PrivateToken;
        _configuredDefaultBranch = connection.DefaultBranch;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get, $"{_baseUrl}/api/v4/projects/{_projectPath}");
            request.Headers.Add("PRIVATE-TOKEN", _privateToken);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return ConnectionProbeResult.Reachable(stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "GitLab source probe failed for {Project}", _projectPath);
            return ConnectionProbeResult.Unreachable(stopwatch.ElapsedMilliseconds, ex.Message);
        }
    }

    public async Task<Repository> CheckoutAsync(
        BranchName? branch, CancellationToken cancellationToken)
    {
        var target = branch ?? new BranchName(await GetDefaultBranchAsync(cancellationToken));
        _logger.LogInformation(
            "Resolved metadata for {Url} on branch {Branch}", _cloneUrl, target);
        return new Repository(target, _cloneUrl);
    }

    public async Task<string> CreatePullRequestAsync(
        Repository repository, string title, string description,
        CancellationToken cancellationToken,
        TicketId? linkedTicketId = null, bool isDraft = false)
    {
        var targetBranch = await GetDefaultBranchAsync(cancellationToken);
        var url = $"{_baseUrl}/api/v4/projects/{_projectPath}/merge_requests";

        // GitLab auto-closes referenced issues on MR merge when the description
        // includes a "Closes #N" / "Closes !N" footer (same syntax as GitHub).
        var body = linkedTicketId is null
            ? description
            : $"{description}\n\nCloses #{linkedTicketId.Value}";

        // GitLab has no draft flag on the API — a "Draft:" title prefix marks the
        // MR as work-in-progress and blocks merge until removed.
        var mrTitle = isDraft ? $"Draft: {title}" : title;

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("PRIVATE-TOKEN", _privateToken);
        request.Content = JsonContent.Create(new
        {
            source_branch = repository.CurrentBranch.Value,
            target_branch = targetBranch,
            title = mrTitle,
            description = body
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        // p0298: a re-run's branch already has an open MR — GitLab 409s. That's not a
        // failure: reuse the existing MR so the ticket doesn't fail at the PR step.
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            return await FindOpenMergeRequestUrlAsync(repository.CurrentBranch.Value, cancellationToken);
        await response.EnsureSuccessWithBodyAsync(cancellationToken);

        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);

        var webUrl = json.RootElement.GetProperty("web_url").GetString()
            ?? throw new ProviderException(ProviderType, "Merge request response did not contain a web_url.");

        _logger.LogInformation("Merge request created: {Url}", webUrl);
        return webUrl;
    }

    private async Task<string> FindOpenMergeRequestUrlAsync(
        string sourceBranch, CancellationToken cancellationToken)
    {
        var url = $"{_baseUrl}/api/v4/projects/{_projectPath}/merge_requests"
            + $"?source_branch={Uri.EscapeDataString(sourceBranch)}&state=opened";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("PRIVATE-TOKEN", _privateToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var first = json.RootElement.EnumerateArray().FirstOrDefault();
        var webUrl = first.ValueKind == JsonValueKind.Object
            && first.TryGetProperty("web_url", out var w) ? w.GetString() : null;
        if (webUrl is null)
            throw new ProviderException(
                ProviderType, $"MR already exists but none found open for source branch '{sourceBranch}'.");

        _logger.LogInformation("Reusing existing merge request: {Url}", webUrl);
        return webUrl;
    }

    public async Task PostCommentAsync(
        string prIdentifier, string markdown, CancellationToken cancellationToken = default)
    {
        var encodedPath = _projectPath;
        var url = $"{_baseUrl}/api/v4/projects/{encodedPath}/merge_requests/{prIdentifier}/notes";
        var json = System.Text.Json.JsonSerializer.Serialize(new { body = markdown });
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(url, content, cancellationToken);
        await response.EnsureSuccessWithBodyAsync(cancellationToken);
        _logger.LogInformation("Posted comment on MR !{MrIid}", prIdentifier);
    }

    // p0167c: GitLab has no batch review-create — inline comments post as one
    // positioned discussion each (position needs the MR's diff_refs shas,
    // fetched once per batch). A position the API rejects (anchor drifted off
    // the diff between compile and post) degrades to a plain MR note so the
    // finding is still delivered and its marker stays deletable.
    public async Task PostReviewBatchAsync(
        string prIdentifier, PrReviewSummary review, CancellationToken cancellationToken = default)
    {
        if (review.InlineComments.Count > 0)
        {
            var diffRefs = await GetDiffRefsAsync(prIdentifier, cancellationToken);
            foreach (var comment in review.InlineComments)
                await PostPositionedNoteAsync(prIdentifier, comment, diffRefs, cancellationToken);
        }
        await PostCommentAsync(prIdentifier, review.TopLevelComment, cancellationToken);
    }

    private async Task PostPositionedNoteAsync(
        string mrIid, PrReviewInlineComment comment,
        (string BaseSha, string StartSha, string HeadSha) diffRefs, CancellationToken cancellationToken)
    {
        var url = $"{_baseUrl}/api/v4/projects/{_projectPath}/merge_requests/{mrIid}/discussions";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("PRIVATE-TOKEN", _privateToken);
        request.Content = JsonContent.Create(new
        {
            body = comment.Body,
            position = new
            {
                position_type = "text",
                base_sha = diffRefs.BaseSha,
                start_sha = diffRefs.StartSha,
                head_sha = diffRefs.HeadSha,
                new_path = comment.File,
                new_line = comment.EndLine,
            },
        });
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode) return;
        _logger.LogWarning(
            "Positioned note for {File}:{Line} on MR !{MrIid} rejected ({Status}) — posting as plain note",
            comment.File, comment.EndLine, mrIid, (int)response.StatusCode);
        await PostCommentAsync(
            mrIid, $"{comment.Body}\n\n_(at `{comment.File}:{comment.StartLine}..{comment.EndLine}`)_",
            cancellationToken);
    }

    private async Task<(string BaseSha, string StartSha, string HeadSha)> GetDiffRefsAsync(
        string mrIid, CancellationToken cancellationToken)
    {
        var url = $"{_baseUrl}/api/v4/projects/{_projectPath}/merge_requests/{mrIid}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("PRIVATE-TOKEN", _privateToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await response.EnsureSuccessWithBodyAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var refs = json.RootElement.GetProperty("diff_refs");
        return (
            refs.GetProperty("base_sha").GetString() ?? throw MissingDiffRef("base_sha"),
            refs.GetProperty("start_sha").GetString() ?? throw MissingDiffRef("start_sha"),
            refs.GetProperty("head_sha").GetString() ?? throw MissingDiffRef("head_sha"));
    }

    private ProviderException MissingDiffRef(string field) =>
        new(ProviderType, $"MR diff_refs did not contain {field} — cannot anchor inline comments.");

    public async Task<int> DeleteCommentsByMarkerAsync(
        string prIdentifier, string markerPrefix, CancellationToken cancellationToken = default)
    {
        var deleted = 0;
        for (var page = 1; ; page++)
        {
            var notes = await GetNotesPageAsync(prIdentifier, page, cancellationToken);
            foreach (var (noteId, body) in notes)
                if (body.StartsWith(markerPrefix, StringComparison.Ordinal))
                {
                    await DeleteNoteAsync(prIdentifier, noteId, cancellationToken);
                    deleted++;
                }
            if (notes.Count < NotesPageSize) break;
        }
        _logger.LogInformation("Deleted {Count} marked note(s) on MR !{MrIid}", deleted, prIdentifier);
        return deleted;
    }

    private const int NotesPageSize = 100;

    private async Task<List<(long Id, string Body)>> GetNotesPageAsync(
        string mrIid, int page, CancellationToken cancellationToken)
    {
        var url = $"{_baseUrl}/api/v4/projects/{_projectPath}/merge_requests/{mrIid}/notes"
            + $"?per_page={NotesPageSize}&page={page}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("PRIVATE-TOKEN", _privateToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await response.EnsureSuccessWithBodyAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        return json.RootElement.EnumerateArray()
            .Select(n => (n.GetProperty("id").GetInt64(),
                n.TryGetProperty("body", out var b) ? b.GetString() ?? "" : ""))
            .ToList();
    }

    private async Task DeleteNoteAsync(string mrIid, long noteId, CancellationToken cancellationToken)
    {
        var url = $"{_baseUrl}/api/v4/projects/{_projectPath}/merge_requests/{mrIid}/notes/{noteId}";
        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Add("PRIVATE-TOKEN", _privateToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await response.EnsureSuccessWithBodyAsync(cancellationToken);
    }

    public async Task<bool> UpdatePullRequestBodyAsync(
        string prUrl, string newBody, CancellationToken cancellationToken)
    {
        if (!TryParseMergeRequestIid(prUrl, out var iid)) return false;
        try
        {
            var encodedProject = _projectPath;
            var url = $"{_baseUrl}/api/v4/projects/{encodedProject}/merge_requests/{iid}";
            using var request = new HttpRequestMessage(HttpMethod.Put, url);
            request.Headers.Add("PRIVATE-TOKEN", _privateToken);
            request.Content = JsonContent.Create(new { description = newBody });
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            _logger.LogInformation("Updated MR body for !{Iid}", iid);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update MR body for !{Iid}", iid);
            return false;
        }
    }

    private static bool TryParseMergeRequestIid(string mrUrl, out int iid)
    {
        iid = 0;
        var match = System.Text.RegularExpressions.Regex.Match(mrUrl, @"/merge_requests/(\d+)");
        return match.Success && int.TryParse(match.Groups[1].Value, out iid);
    }

    public async Task<string?> TryReadFileAsync(string path, CancellationToken cancellationToken)
    {
        var branch = await GetDefaultBranchAsync(cancellationToken);
        // GitLab REST: /projects/:id/repository/files/<urlencoded-path>/raw?ref=<branch>.
        // _projectPath is ALREADY url-escaped by the factory (namespace%2Fname), so it goes
        // in raw — re-escaping double-encodes it (%252F) and 404s nested-subgroup projects.
        // Only the file `path` segment is escaped here.
        var encodedProject = _projectPath;
        var encodedPath = Uri.EscapeDataString(path);
        var url = $"{_baseUrl}/api/v4/projects/{encodedProject}/repository/files/{encodedPath}/raw?ref={Uri.EscapeDataString(branch)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("PRIVATE-TOKEN", _privateToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ListDirectoryAsync(string path, CancellationToken cancellationToken)
    {
        var branch = await GetDefaultBranchAsync(cancellationToken);
        // GitLab REST: /projects/:id/repository/tree?path=<dir>&ref=<branch>.
        // Returns a JSON array of { id, name, type, path, mode }. We project name only.
        // No pagination handling for now — the .agentsmith/contexts/ directory in
        // realistic monorepos is small (sub-package count, dozens max).
        var encodedProject = _projectPath;
        var url = $"{_baseUrl}/api/v4/projects/{encodedProject}/repository/tree?path={Uri.EscapeDataString(path)}&ref={Uri.EscapeDataString(branch)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("PRIVATE-TOKEN", _privateToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return [];
        response.EnsureSuccessStatusCode();

        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        var names = new List<string>();
        foreach (var item in json.RootElement.EnumerateArray())
        {
            if (item.TryGetProperty("name", out var name) && name.GetString() is { } n)
                names.Add(n);
        }
        return names;
    }

    private async Task<string> GetDefaultBranchAsync(CancellationToken cancellationToken)
    {
        if (_configuredDefaultBranch is not null)
            return _configuredDefaultBranch;

        if (_cachedDefaultBranch is not null)
            return _cachedDefaultBranch;

        try
        {
            var url = $"{_baseUrl}/api/v4/projects/{_projectPath}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("PRIVATE-TOKEN", _privateToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var json = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);

            _cachedDefaultBranch = json.RootElement.TryGetProperty("default_branch", out var branch)
                ? branch.GetString() ?? "main"
                : "main";

            _logger.LogDebug("Resolved default branch from GitLab API: {Branch}", _cachedDefaultBranch);
            return _cachedDefaultBranch;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve default branch from GitLab API, falling back to 'main'");
            _cachedDefaultBranch = "main";
            return _cachedDefaultBranch;
        }
    }
}
