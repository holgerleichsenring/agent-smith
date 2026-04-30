using System.Net;
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
/// p95b ships Label-mode only — updates fields.labels via PUT. Native-mode
/// (POST /transitions) lands in p95c once LifecycleConfig defines the workflow
/// status names. Concurrent-writer serialization is the decorator's concern
/// (LockedTicketStatusTransitioner, Server-only) — Jira labels are not atomic
/// (no If-Match), but a single CLI process cannot race with itself, so the lock
/// only attaches in the multi-pod Server composition.
/// </summary>
public sealed class JiraTicketStatusTransitioner(
    string baseUrl,
    string email,
    string apiToken,
    string projectKey,
    JiraWorkflowCatalog catalog,
    HttpClient httpClient,
    ILogger<JiraTicketStatusTransitioner> logger) : ITicketStatusTransitioner
{
    public string ProviderType => "Jira";

    public async Task<TicketLifecycleStatus?> ReadCurrentAsync(
        TicketId ticketId, CancellationToken cancellationToken)
    {
        var labels = await FetchLabelsAsync(ticketId, cancellationToken);
        return labels is null ? null : ParseLifecycle(labels);
    }

    public async Task<TransitionResult> TransitionAsync(
        TicketId ticketId, TicketLifecycleStatus from,
        TicketLifecycleStatus to, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Jira Transition #{Ticket}: {From} → {To}", ticketId.Value, from, to);

        var mode = catalog.GetModeForProject(projectKey);
        if (mode == JiraLifecycleMode.Native)
        {
            logger.LogWarning(
                "Jira Transition #{Ticket}: Native-mode not implemented", ticketId.Value);
            return TransitionResult.Failed("Native-mode transitions land in p95c");
        }

        var result = await TransitionViaLabelsAsync(ticketId, from, to, cancellationToken);
        logger.LogInformation(
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
            logger.LogWarning("Jira Transition #{Ticket}: ticket not found", ticketId.Value);
            return TransitionResult.NotFound();
        }

        var current = ParseLifecycle(labels);
        if (!Matches(current, from))
        {
            logger.LogWarning(
                "Jira Transition #{Ticket}: precondition failed (expected {From}, found {Current})",
                ticketId.Value, from, current?.ToString() ?? "<none>");
            return TransitionResult.PreconditionFailed(
                $"Expected {from}, found {current?.ToString() ?? "<none>"}");
        }

        return await PutLabelsAsync(ticketId, current, to, ct);
    }

    private async Task<string[]?> FetchLabelsAsync(TicketId ticketId, CancellationToken ct)
    {
        var url = $"{baseUrl.TrimEnd('/')}/rest/api/3/issue/{ticketId.Value}?fields=labels";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        SetAuth(req);
        using var resp = await httpClient.SendAsync(req, ct);
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
        var url = $"{baseUrl.TrimEnd('/')}/rest/api/3/issue/{ticketId.Value}";

        using var req = new HttpRequestMessage(HttpMethod.Put, url);
        SetAuth(req);
        req.Content = new StringContent(
            JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var resp = await httpClient.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var details = await resp.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Jira label update failed: {Status} {Body}", resp.StatusCode, details);
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

    private static bool Matches(TicketLifecycleStatus? current, TicketLifecycleStatus expected)
        => expected == TicketLifecycleStatus.Pending
            ? current is null or TicketLifecycleStatus.Pending
            : current == expected;

    private static TicketLifecycleStatus? ParseLifecycle(string[] labels)
    {
        foreach (var label in labels)
            if (LifecycleLabels.TryParse(label, out var status))
                return status;
        return null;
    }

    private void SetAuth(HttpRequestMessage request)
    {
        var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{apiToken}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", creds);
    }
}
