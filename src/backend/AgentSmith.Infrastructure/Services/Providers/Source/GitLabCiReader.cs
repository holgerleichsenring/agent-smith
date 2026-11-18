using System.Globalization;
using System.Text.Json;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Services.Providers.Pipelines;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-17-3435a: a GitLab merge request's CI — its head pipeline, decided per pipeline as GitLab does (a running
/// pipeline with a failed job is pending until GitLab calls it failed). A red one is read with its failed jobs through
/// <see cref="GitLabPipelineFailures"/> (2026-11-18-3863e), as a deploy's pipeline is.
/// </summary>
public sealed class GitLabCiReader(string baseUrl, string projectPath, string token, HttpClient http)
{
    private string Api => $"{baseUrl}/api/v4/projects/{projectPath}";

    public async Task<PullRequestCi> ReadAsync(string prUrl, CiReadRequest request, CancellationToken ct)
    {
        if (!GitLabMergeRequestUpdater.TryParseMergeRequestIid(prUrl, out var iid))
            throw new ArgumentException($"Not a GitLab merge request URL: {prUrl}", nameof(prUrl));
        using var mr = await GetJsonAsync($"{Api}/merge_requests/{iid}", ct);
        var head = request.HeadSha ?? Str(mr.RootElement, "sha");
        if (!mr.RootElement.TryGetProperty("head_pipeline", out var pipeline) || pipeline.ValueKind != JsonValueKind.Object)
            return PullRequestCi.NoneOn(head);
        var at = Time(pipeline, "finished_at") ?? Time(pipeline, "updated_at");
        var outcome = Str(pipeline, "sha") != head ? CiCheckOutcome.Pending : GitLabPipelineStatus.Outcome(Str(pipeline, "status"));
        var state = CiVerdict.Of([new CiCheck("pipeline", outcome, false, at)]);
        return new PullRequestCi(state, head, at, state == CiStates.Red
            ? await new GitLabPipelineFailures(new PipelineHttp(http, r => r.Headers.Add("PRIVATE-TOKEN", token)), Api)
                .ReadAsync(pipeline.GetProperty("id").GetInt64(), Str(pipeline, "web_url"), request, ct) : []);
    }

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken ct)
    {
        using var request = Get(url);
        using var response = await http.SendAsync(request, ct);
        await response.EnsureSuccessWithBodyAsync(ct);
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
    }

    private HttpRequestMessage Get(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("PRIVATE-TOKEN", token);
        return request;
    }

    private static DateTimeOffset? Time(JsonElement e, string name) =>
        Str(e, name) is { } at ? DateTimeOffset.Parse(at, CultureInfo.InvariantCulture) : null;

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
