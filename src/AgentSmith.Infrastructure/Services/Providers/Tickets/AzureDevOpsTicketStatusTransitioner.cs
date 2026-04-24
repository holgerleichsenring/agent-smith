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
/// Azure DevOps lifecycle transitioner. Uses JSON Patch on /workitems/{id} with the
/// work item's `rev` as If-Match for optimistic concurrency — 412 on rev mismatch.
/// Lifecycle is carried in System.Tags (semicolon-separated tag list).
/// </summary>
public sealed class AzureDevOpsTicketStatusTransitioner(
    string orgUrl,
    string project,
    string personalAccessToken,
    HttpClient httpClient,
    ILogger<AzureDevOpsTicketStatusTransitioner> logger) : ITicketStatusTransitioner
{
    public string ProviderType => "AzureDevOps";

    public async Task<TicketLifecycleStatus?> ReadCurrentAsync(
        TicketId ticketId, CancellationToken cancellationToken)
    {
        var (tags, _) = await FetchTagsAsync(ticketId, cancellationToken);
        return tags is null ? null : ParseLifecycle(tags);
    }

    public async Task<TransitionResult> TransitionAsync(
        TicketId ticketId, TicketLifecycleStatus from,
        TicketLifecycleStatus to, CancellationToken cancellationToken)
    {
        var (tags, rev) = await FetchTagsAsync(ticketId, cancellationToken);
        if (tags is null) return TransitionResult.NotFound();

        var current = ParseLifecycle(tags);
        if (!Matches(current, from))
            return TransitionResult.PreconditionFailed(
                $"Expected {from}, found {current?.ToString() ?? "<none>"}");

        var newTags = BuildTags(tags, to);
        return await PatchTagsAsync(ticketId, newTags, rev, cancellationToken);
    }

    private async Task<(string[]?, int)> FetchTagsAsync(TicketId ticketId, CancellationToken ct)
    {
        var url = $"{orgUrl.TrimEnd('/')}/{project}/_apis/wit/workitems/{ticketId.Value}?fields=System.Tags,System.Rev&api-version=7.0";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        SetAuth(req);
        using var resp = await httpClient.SendAsync(req, ct);
        if (resp.StatusCode == HttpStatusCode.NotFound) return (null, 0);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var fields = doc.RootElement.GetProperty("fields");
        var tagsStr = fields.TryGetProperty("System.Tags", out var t) ? t.GetString() ?? "" : "";
        var rev = fields.TryGetProperty("System.Rev", out var r) ? r.GetInt32() : 0;
        var tags = tagsStr.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return (tags, rev);
    }

    private async Task<TransitionResult> PatchTagsAsync(
        TicketId ticketId, string[] newTags, int rev, CancellationToken ct)
    {
        var url = $"{orgUrl.TrimEnd('/')}/{project}/_apis/wit/workitems/{ticketId.Value}?api-version=7.0";
        var patch = new object[]
        {
            new { op = "test", path = "/rev", value = (object)rev },
            new { op = "add", path = "/fields/System.Tags", value = (object)string.Join("; ", newTags) }
        };

        using var req = new HttpRequestMessage(HttpMethod.Patch, url);
        SetAuth(req);
        req.Content = new StringContent(
            JsonSerializer.Serialize(patch), Encoding.UTF8, "application/json-patch+json");

        using var resp = await httpClient.SendAsync(req, ct);
        if (resp.StatusCode == HttpStatusCode.PreconditionFailed || resp.StatusCode == HttpStatusCode.Conflict)
            return TransitionResult.PreconditionFailed("rev mismatch");
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            logger.LogWarning("AzDO PATCH failed: {Status} {Body}", resp.StatusCode, body);
            return TransitionResult.Failed($"HTTP {(int)resp.StatusCode}");
        }
        return TransitionResult.Succeeded();
    }

    private static string[] BuildTags(string[] current, TicketLifecycleStatus to)
    {
        var filtered = current.Where(t => !LifecycleLabels.IsLifecycleLabel(t)).ToList();
        filtered.Add(LifecycleLabels.For(to));
        return [.. filtered];
    }

    private static bool Matches(TicketLifecycleStatus? current, TicketLifecycleStatus expected)
        => expected == TicketLifecycleStatus.Pending
            ? current is null or TicketLifecycleStatus.Pending
            : current == expected;

    private static TicketLifecycleStatus? ParseLifecycle(string[] tags)
    {
        foreach (var tag in tags)
            if (LifecycleLabels.TryParse(tag, out var status))
                return status;
        return null;
    }

    private void SetAuth(HttpRequestMessage request)
    {
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($":{personalAccessToken}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
    }
}
