using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// Atomic GitHub Issues lifecycle transitioner. Reads the issue with its ETag,
/// verifies the 'from' label is present, then PATCHes the labels array with If-Match.
/// A 412 Precondition Failed means another process changed the issue between read and write.
/// </summary>
public sealed class GitHubTicketStatusTransitioner : ITicketStatusTransitioner
{
    private readonly string _owner;
    private readonly string _repo;
    private readonly HttpClient _http;
    private readonly ILogger<GitHubTicketStatusTransitioner> _logger;

    public string ProviderType => "GitHub";

    public GitHubTicketStatusTransitioner(
        string repoUrl, string token, HttpClient httpClient,
        ILogger<GitHubTicketStatusTransitioner> logger)
    {
        (_owner, _repo) = ParseGitHubUrl(repoUrl);
        _http = httpClient;
        _logger = logger;
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("AgentSmith/1.0");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    public async Task<TicketLifecycleStatus?> ReadCurrentAsync(
        TicketId ticketId, CancellationToken cancellationToken)
    {
        var (issue, _) = await FetchIssueAsync(ticketId, cancellationToken);
        return issue is null ? null : ReadLifecycleLabel(issue.Value);
    }

    public async Task<TransitionResult> TransitionAsync(
        TicketId ticketId, TicketLifecycleStatus from,
        TicketLifecycleStatus to, CancellationToken cancellationToken)
    {
        var (issue, etag) = await FetchIssueAsync(ticketId, cancellationToken);
        if (issue is null) return TransitionResult.NotFound();

        var current = ReadLifecycleLabel(issue.Value);
        if (!Matches(current, from))
            return TransitionResult.PreconditionFailed(
                $"Expected {from}, found {(current?.ToString() ?? "<none>")}");

        var newLabels = BuildLabels(issue.Value, to);
        return await PatchLabelsAsync(ticketId, newLabels, etag, cancellationToken);
    }

    private static bool Matches(TicketLifecycleStatus? current, TicketLifecycleStatus expected)
        => expected == TicketLifecycleStatus.Pending
            ? current is null or TicketLifecycleStatus.Pending
            : current == expected;

    private async Task<(JsonElement?, string?)> FetchIssueAsync(
        TicketId ticketId, CancellationToken ct)
    {
        var url = $"https://api.github.com/repos/{_owner}/{_repo}/issues/{ticketId.Value}";
        var resp = await _http.GetAsync(url, ct);
        if (resp.StatusCode == HttpStatusCode.NotFound) return (null, null);
        resp.EnsureSuccessStatusCode();
        var etag = resp.Headers.ETag?.Tag;
        var json = await resp.Content.ReadAsStringAsync(ct);
        return (JsonDocument.Parse(json).RootElement, etag);
    }

    private async Task<TransitionResult> PatchLabelsAsync(
        TicketId ticketId, string[] labels, string? etag, CancellationToken ct)
    {
        var url = $"https://api.github.com/repos/{_owner}/{_repo}/issues/{ticketId.Value}";
        var body = JsonSerializer.Serialize(new { labels });
        using var req = new HttpRequestMessage(HttpMethod.Patch, url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrEmpty(etag))
            req.Headers.TryAddWithoutValidation("If-Match", etag);

        var resp = await _http.SendAsync(req, ct);
        if (resp.StatusCode == HttpStatusCode.PreconditionFailed)
            return TransitionResult.PreconditionFailed("ETag mismatch");
        if (!resp.IsSuccessStatusCode)
        {
            var details = await resp.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("PATCH labels failed: {Status} {Body}", resp.StatusCode, details);
            return TransitionResult.Failed($"HTTP {(int)resp.StatusCode}");
        }
        return TransitionResult.Succeeded();
    }

    private static TicketLifecycleStatus? ReadLifecycleLabel(JsonElement issue)
    {
        if (!issue.TryGetProperty("labels", out var labels)) return null;
        foreach (var label in labels.EnumerateArray())
        {
            if (!label.TryGetProperty("name", out var nameEl)) continue;
            var name = nameEl.GetString();
            if (name is not null && LifecycleLabels.TryParse(name, out var status))
                return status;
        }
        return null;
    }

    private static string[] BuildLabels(JsonElement issue, TicketLifecycleStatus to)
    {
        var result = new List<string>();
        if (issue.TryGetProperty("labels", out var labels))
        {
            foreach (var label in labels.EnumerateArray())
            {
                if (!label.TryGetProperty("name", out var nameEl)) continue;
                var name = nameEl.GetString();
                if (name is null || LifecycleLabels.IsLifecycleLabel(name)) continue;
                result.Add(name);
            }
        }
        result.Add(LifecycleLabels.For(to));
        return [.. result];
    }

    private static (string owner, string repo) ParseGitHubUrl(string url)
    {
        var uri = new Uri(url);
        var segments = uri.AbsolutePath.Trim('/').Split('/');
        if (segments.Length < 2)
            throw new ConfigurationException($"Invalid GitHub URL: {url}");
        return (segments[0], segments[1]);
    }
}
