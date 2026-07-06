using System.Net;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Tickets;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// Jira lifecycle transitioner. Delegates mode selection to JiraWorkflowCatalog.
/// Label-mode updates fields.labels via PUT. Native-mode (p0300a) drives the
/// operator-named workflow statuses via POST /transitions and falls back to labels
/// for any unmapped state or unmatched transition, so a lifecycle change is never
/// silently lost. Concurrent-writer serialization is the decorator's concern
/// (LockedTicketStatusTransitioner, Server-only) — Jira labels are not atomic
/// (no If-Match), but a single CLI process cannot race with itself, so the lock
/// only attaches in the multi-pod Server composition.
/// </summary>
public sealed class JiraTicketStatusTransitioner : ITicketStatusTransitioner
{
    private readonly string _baseUrl;
    private readonly string _email;
    private readonly string _apiToken;
    private readonly string _projectKey;
    private readonly Contracts.Models.Configuration.JiraEndpoints _endpoints;
    private readonly JiraWorkflowCatalog _catalog;
    private readonly HttpClient _httpClient;
    private readonly ILogger<JiraTicketStatusTransitioner> _logger;
    private readonly JiraNativeLifecycleTransitioner? _native;

    public JiraTicketStatusTransitioner(
        JiraTicketConnection connection,
        JiraWorkflowCatalog catalog,
        HttpClient httpClient,
        ILogger<JiraTicketStatusTransitioner> logger)
    {
        _baseUrl = connection.BaseUrl.TrimEnd('/');
        _email = connection.Email;
        _apiToken = connection.ApiToken;
        _projectKey = connection.ProjectKey ?? "default";
        _endpoints = connection.ResolvedEndpoints;
        _catalog = catalog;
        _httpClient = httpClient;
        _logger = logger;

        var map = connection.ResolvedLifecycleMap;
        _native = map.IsEmpty ? null : BuildNativeTransitioner(map, logger);
    }

    private JiraNativeLifecycleTransitioner BuildNativeTransitioner(
        Contracts.Models.Configuration.JiraLifecycleStatusMap map, ILogger logger)
    {
        var http = TicketProviderHttpClient.WithBasicAuth(_httpClient, _email, _apiToken);
        return new JiraNativeLifecycleTransitioner(
            new JiraTransitioner(http, _baseUrl, _endpoints, logger), map);
    }

    public string ProviderType => "Jira";

    public async Task<TicketLifecycleStatus?> ReadCurrentAsync(
        TicketId ticketId, CancellationToken cancellationToken)
    {
        using var scope = _logger.BeginScope("ticket={Ticket}", ticketId.Value);
        var labels = await FetchLabelsAsync(ticketId, cancellationToken);
        return labels is null ? null : ParseLifecycle(labels);
    }

    public async Task<TransitionResult> TransitionAsync(
        TicketId ticketId, TicketLifecycleStatus from,
        TicketLifecycleStatus to, CancellationToken cancellationToken)
    {
        using var scope = _logger.BeginScope("ticket={Ticket}", ticketId.Value);
        _logger.LogInformation(
            "Jira Transition #{Ticket}: {From} → {To}", ticketId.Value, from, to);

        var mode = _catalog.GetModeForProject(_projectKey, _native is not null);
        if (mode == JiraLifecycleMode.Native
            && await _native!.TryTransitionAsync(ticketId, to, cancellationToken))
        {
            _logger.LogInformation("Jira Transition #{Ticket}: Succeeded (native)", ticketId.Value);
            return TransitionResult.Succeeded();
        }

        // Label mode, or native mode with no mapped/matching transition: labels are the
        // always-available record carrier so the lifecycle change is never silently lost.
        var result = await TransitionViaLabelsAsync(ticketId, from, to, cancellationToken);
        _logger.LogInformation(
            "Jira Transition #{Ticket}: {Outcome}", ticketId.Value, result.Outcome);
        return result;
    }

    private async Task<TransitionResult> TransitionViaLabelsAsync(
        TicketId ticketId, TicketLifecycleStatus from,
        TicketLifecycleStatus to, CancellationToken ct)
    {
        var labels = await FetchLabelsAsync(ticketId, ct);
        if (labels is null)
        {
            _logger.LogWarning("Jira Transition #{Ticket}: ticket not found", ticketId.Value);
            return TransitionResult.NotFound();
        }

        // p0262: lifecycle tags are pure markers — set `to` unconditionally, no `from`
        // precondition. `current` is still read to strip the old lifecycle label; `from`
        // is advisory. Concurrent-writer serialization is the decorator's Redis lock;
        // run-level single-run is the lease's job (p0246b).
        var current = ParseLifecycle(labels);
        return await PutLabelsAsync(ticketId, current, to, ct);
    }

    private async Task<string[]?> FetchLabelsAsync(TicketId ticketId, CancellationToken ct)
    {
        var url = $"{_baseUrl}{_endpoints.IssueFor(ticketId.Value)}?fields=labels";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        SetAuth(req);
        using var resp = await _httpClient.SendAsync(req, ct);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        if (!doc.RootElement.TryGetProperty("fields", out var fields)
            || !fields.TryGetProperty("labels", out var labels))
            return [];
        return labels.EnumerateArray().Select(e => e.GetString() ?? "").ToArray();
    }

    private async Task<TransitionResult> PutLabelsAsync(
        TicketId ticketId, TicketLifecycleStatus? current, TicketLifecycleStatus to, CancellationToken ct)
    {
        var newLabels = BuildLabels(current, to);
        var body = new { update = new { labels = BuildLabelOps(current, to) } };
        var url = $"{_baseUrl}{_endpoints.IssueFor(ticketId.Value)}";

        using var req = new HttpRequestMessage(HttpMethod.Put, url);
        SetAuth(req);
        req.Content = new StringContent(
            JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var resp = await _httpClient.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var details = await resp.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Jira label update failed: {Status} {Body}", resp.StatusCode, details);
            return TransitionResult.Failed($"HTTP {(int)resp.StatusCode}");
        }
        _ = newLabels;
        return TransitionResult.Succeeded();
    }

    private static object[] BuildLabelOps(TicketLifecycleStatus? current, TicketLifecycleStatus to)
    {
        var ops = new List<object> { new { add = LifecycleLabels.For(to) } };
        if (current is not null) ops.Add(new { remove = LifecycleLabels.For(current.Value) });
        return [.. ops];
    }

    private static string[] BuildLabels(TicketLifecycleStatus? current, TicketLifecycleStatus to)
    {
        var list = new List<string> { LifecycleLabels.For(to) };
        if (current is not null) list.Remove(LifecycleLabels.For(current.Value));
        return [.. list];
    }


    private static TicketLifecycleStatus? ParseLifecycle(string[] labels)
    {
        foreach (var label in labels)
            if (LifecycleLabels.TryParse(label, out var status))
                return status;
        return null;
    }

    private void SetAuth(HttpRequestMessage request)
    {
        var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_email}:{_apiToken}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", creds);
    }
}
