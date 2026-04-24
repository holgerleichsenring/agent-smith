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
/// p95b ships Label-mode only — updates fields.labels via PUT. Takes an extra SETNX
/// label-lock around the PATCH because Jira labels are not atomic (no If-Match),
/// layered on top of the global claim-lock that TicketClaimService already holds.
/// Native-mode (POST /transitions) lands in p95c once LifecycleConfig defines the
/// workflow status names.
/// </summary>
public sealed class JiraTicketStatusTransitioner(
    string baseUrl,
    string email,
    string apiToken,
    string projectKey,
    JiraWorkflowCatalog catalog,
    IRedisClaimLock labelLock,
    HttpClient httpClient,
    ILogger<JiraTicketStatusTransitioner> logger) : ITicketStatusTransitioner
{
    private static readonly TimeSpan LabelLockTtl = TimeSpan.FromSeconds(10);

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
        var mode = catalog.GetModeForProject(projectKey);
        if (mode == JiraLifecycleMode.Native)
            return TransitionResult.Failed("Native-mode transitions land in p95c");

        return await TransitionViaLabelsAsync(ticketId, from, to, cancellationToken);
    }

    private async Task<TransitionResult> TransitionViaLabelsAsync(
        TicketId ticketId, TicketLifecycleStatus from,
        TicketLifecycleStatus to, CancellationToken ct)
    {
        var lockKey = $"agentsmith:jira-label-lock:{ticketId.Value}";
        var token = await labelLock.TryAcquireAsync(lockKey, LabelLockTtl, ct);
        if (token is null) return TransitionResult.PreconditionFailed("label-lock held");

        try
        {
            var labels = await FetchLabelsAsync(ticketId, ct);
            if (labels is null) return TransitionResult.NotFound();

            var current = ParseLifecycle(labels);
            if (!Matches(current, from))
                return TransitionResult.PreconditionFailed(
                    $"Expected {from}, found {current?.ToString() ?? "<none>"}");

            return await PutLabelsAsync(ticketId, current, to, ct);
        }
        finally
        {
            await labelLock.ReleaseAsync(lockKey, token, ct);
        }
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
