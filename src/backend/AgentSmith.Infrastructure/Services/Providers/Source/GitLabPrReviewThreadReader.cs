using System.Text.Json;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Webhooks;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-08-e8b9d: a GitLab merge request's review from its discussions, paged by 100 up to ten
/// pages. A discussion is resolved when every resolvable note in it is; a discussion with none is
/// not resolvable. Whether an author is a bot is read once per author from the users API.
/// </summary>
public sealed class GitLabPrReviewThreadReader(
    string baseUrl, string projectPath, string token, string repoUrl, HttpClient http, ILogger logger) : IPrReviewThreadReader
{
    private const int PageSize = 100;
    private const int MaxPages = 10;
    private readonly Dictionary<long, bool> _bots = [];

    public async Task<IReadOnlyList<PrReviewThread>> ListAsync(string prUrl, CancellationToken cancellationToken)
    {
        if (!GitLabMergeRequestUpdater.TryParseMergeRequestIid(prUrl, out var iid))
            throw new ArgumentException($"Not a GitLab merge request URL: {prUrl}", nameof(prUrl));
        var threads = new List<PrReviewThread>();
        for (var page = 1; page <= MaxPages; page++)
        {
            using var doc = await GetAsync($"merge_requests/{iid}/discussions?per_page={PageSize}&page={page}", cancellationToken);
            foreach (var discussion in doc.RootElement.EnumerateArray())
                threads.Add(await ThreadAsync(discussion, cancellationToken));
            if (doc.RootElement.GetArrayLength() < PageSize) return threads;
        }
        logger.LogWarning("MR !{Iid} has more than {Max} discussions; the first are read", iid, MaxPages * PageSize);
        return threads;
    }

    private async Task<PrReviewThread> ThreadAsync(JsonElement discussion, CancellationToken ct)
    {
        var notes = discussion.GetProperty("notes").EnumerateArray().ToList();
        var resolvable = notes.Where(n => Bool(n, "resolvable")).ToList();
        var position = notes.Select(n => n.TryGetProperty("position", out var p) && p.ValueKind == JsonValueKind.Object ? p : (JsonElement?)null)
            .FirstOrDefault(p => p is not null);
        var read = new List<PrReviewNote>();
        foreach (var note in notes) read.Add(await NoteAsync(note, ct));
        return new PrReviewThread(position is { } p1 ? Str(p1, "new_path") : null,
            position is { } p2 && p2.TryGetProperty("new_line", out var l) && l.ValueKind == JsonValueKind.Number ? l.GetInt32() : null,
            resolvable.Count == 0 ? null : resolvable.All(n => Bool(n, "resolved")), read);
    }

    private async Task<PrReviewNote> NoteAsync(JsonElement note, CancellationToken ct)
    {
        var author = note.GetProperty("author");
        var id = author.GetProperty("id").GetInt64();
        var login = Str(author, "username") ?? id.ToString();
        return new PrReviewNote(new PrCommentAuthor(repoUrl, projectPath, id.ToString(), login),
            await IsBotAsync(id, ct), Bool(note, "system"),
            DateTimeOffset.Parse(Str(note, "created_at")!), Str(note, "body") ?? string.Empty);
    }

    private async Task<bool> IsBotAsync(long userId, CancellationToken ct)
    {
        if (_bots.TryGetValue(userId, out var known)) return known;
        using var user = await GetUserAsync(userId, ct);
        return _bots[userId] = user is not null && Bool(user.RootElement, "bot");
    }

    private async Task<JsonDocument?> GetUserAsync(long userId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/v4/users/{userId}");
        request.Headers.Add("PRIVATE-TOKEN", token);
        using var response = await http.SendAsync(request, ct);
        return response.IsSuccessStatusCode ? await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct) : null;
    }

    private async Task<JsonDocument> GetAsync(string path, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/v4/projects/{projectPath}/{path}");
        request.Headers.Add("PRIVATE-TOKEN", token);
        using var response = await http.SendAsync(request, ct);
        await response.EnsureSuccessWithBodyAsync(ct);
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
    }

    private static bool Bool(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
