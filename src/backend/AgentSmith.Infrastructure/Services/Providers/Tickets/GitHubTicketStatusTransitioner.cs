using System.Net;
using AgentSmith.Contracts.Tickets;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>GitHub Issues lifecycle transitioner: reads the issue's labels and PATCHes the labels
/// array with the new lifecycle label. The write is unconditional — GitHub refuses conditional
/// headers on writes with 400 (2026-11-25-7f25b) — and last-write-wins: a label a person adds
/// between the GET and the PATCH can be lost. Single-run is the active-run lease's job (p0246b).</summary>
public sealed class GitHubTicketStatusTransitioner : ITicketStatusTransitioner
{
    private readonly string _issues;
    private readonly HttpClient _http;
    private readonly ILogger<GitHubTicketStatusTransitioner> _logger;
    private readonly TicketLabelVocabulary _labels;

    public string ProviderType => "GitHub";

    public GitHubTicketStatusTransitioner(
        GitHubTicketConnection connection, HttpClient httpClient,
        ILogger<GitHubTicketStatusTransitioner> logger)
    {
        var address = GitHubRepoAddress.Parse(connection.RepoUrl);
        _issues = $"{address.Api.RestRoot}/repos/{address.FullName}/issues";
        _http = httpClient;
        _logger = logger;
        _labels = connection.ResolvedLabels;
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", connection.Token);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("AgentSmith/1.0");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    public async Task<TicketLifecycleStatus?> ReadCurrentAsync(
        TicketId ticketId, CancellationToken cancellationToken)
    {
        using var scope = _logger.BeginScope("ticket={Ticket}", ticketId.Value);
        var issue = await FetchIssueAsync(ticketId, cancellationToken);
        return issue is null ? null : ReadLifecycleLabel(issue.Value);
    }

    public async Task<TransitionResult> TransitionAsync(
        TicketId ticketId, TicketLifecycleStatus from,
        TicketLifecycleStatus to, CancellationToken cancellationToken)
    {
        using var scope = _logger.BeginScope("ticket={Ticket}", ticketId.Value);
        _logger.LogInformation(
            "GitHub Transition #{Ticket}: {From} → {To}", ticketId.Value, from, to);

        var issue = await FetchIssueAsync(ticketId, cancellationToken);
        if (issue is null)
        {
            _logger.LogWarning("GitHub Transition #{Ticket}: ticket not found", ticketId.Value);
            return TransitionResult.NotFound();
        }

        // p0262: lifecycle tags are pure markers — set `to` unconditionally, no `from`
        // precondition. `from` is advisory, and run-level single-run is the lease's job
        // (p0246b). 2026-11-25-7f25b: the PATCH carries no If-Match either; GitHub answers a
        // conditional header on a write with 400, which failed every claim.
        var newLabels = BuildLabels(issue.Value, to);
        var result = await PatchLabelsAsync(ticketId, newLabels, cancellationToken);
        _logger.LogInformation(
            "GitHub Transition #{Ticket}: {Outcome}", ticketId.Value, result.Outcome);
        return result;
    }

    private async Task<JsonElement?> FetchIssueAsync(TicketId ticketId, CancellationToken ct)
    {
        var url = $"{_issues}/{ticketId.Value}";
        var resp = await _http.GetAsync(url, ct);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);
        return JsonDocument.Parse(json).RootElement;
    }

    private async Task<TransitionResult> PatchLabelsAsync(
        TicketId ticketId, string[] labels, CancellationToken ct)
    {
        var url = $"{_issues}/{ticketId.Value}";
        var body = JsonSerializer.Serialize(new { labels });
        using var req = new HttpRequestMessage(HttpMethod.Patch, url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var details = await resp.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("PATCH labels failed: {Status} {Body}", resp.StatusCode, details);
            return TransitionResult.Failed($"HTTP {(int)resp.StatusCode}");
        }
        return TransitionResult.Succeeded();
    }

    private TicketLifecycleStatus? ReadLifecycleLabel(JsonElement issue)
    {
        if (!issue.TryGetProperty("labels", out var labels)) return null;
        foreach (var label in labels.EnumerateArray())
        {
            if (!label.TryGetProperty("name", out var nameEl)) continue;
            var name = nameEl.GetString();
            if (name is not null && _labels.TryParse(name, out var status))
                return status;
        }
        return null;
    }

    private string[] BuildLabels(JsonElement issue, TicketLifecycleStatus to)
    {
        var result = new List<string>();
        if (issue.TryGetProperty("labels", out var labels))
        {
            foreach (var label in labels.EnumerateArray())
            {
                if (!label.TryGetProperty("name", out var nameEl)) continue;
                var name = nameEl.GetString();
                if (name is null || _labels.IsLifecycleLabel(name)) continue;
                result.Add(name);
            }
        }
        result.Add(_labels.For(to));
        return [.. result];
    }
}
