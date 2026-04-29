using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// GitLab lifecycle transitioner. GET the issue, verify the 'from' label is present,
/// then PUT with add_labels/remove_labels (targets only lifecycle labels, leaves others
/// untouched). GitLab has no ETag for issues — concurrency is last-write-wins at the
/// GitLab level; the TicketClaimService's SETNX claim-lock is the primary race guard.
/// </summary>
public sealed class GitLabTicketStatusTransitioner(
    string baseUrl,
    string projectPath,
    string privateToken,
    HttpClient httpClient,
    ILogger<GitLabTicketStatusTransitioner> logger) : ITicketStatusTransitioner
{
    public string ProviderType => "GitLab";

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
            "GitLab Transition #{Ticket}: {From} → {To}", ticketId.Value, from, to);

        var labels = await FetchLabelsAsync(ticketId, cancellationToken);
        if (labels is null)
        {
            logger.LogWarning("GitLab Transition #{Ticket}: ticket not found", ticketId.Value);
            return TransitionResult.NotFound();
        }

        var current = ParseLifecycle(labels);
        if (!Matches(current, from))
        {
            logger.LogWarning(
                "GitLab Transition #{Ticket}: precondition failed (expected {From}, found {Current})",
                ticketId.Value, from, current?.ToString() ?? "<none>");
            return TransitionResult.PreconditionFailed(
                $"Expected {from}, found {current?.ToString() ?? "<none>"}");
        }

        var result = await UpdateLabelsAsync(ticketId, current, to, cancellationToken);
        logger.LogInformation(
            "GitLab Transition #{Ticket}: {Outcome}", ticketId.Value, result.Outcome);
        return result;
    }

    private async Task<string[]?> FetchLabelsAsync(TicketId ticketId, CancellationToken ct)
    {
        var url = $"{baseUrl.TrimEnd('/')}/api/v4/projects/{projectPath}/issues/{ticketId.Value}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("PRIVATE-TOKEN", privateToken);

        using var resp = await httpClient.SendAsync(req, ct);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("labels", out var labelsEl)) return [];
        return labelsEl.EnumerateArray().Select(e => e.GetString() ?? "").ToArray();
    }

    private async Task<TransitionResult> UpdateLabelsAsync(
        TicketId ticketId, TicketLifecycleStatus? current, TicketLifecycleStatus to, CancellationToken ct)
    {
        var url = $"{baseUrl.TrimEnd('/')}/api/v4/projects/{projectPath}/issues/{ticketId.Value}";
        using var req = new HttpRequestMessage(HttpMethod.Put, url);
        req.Headers.Add("PRIVATE-TOKEN", privateToken);
        req.Content = JsonContent.Create(new
        {
            add_labels = LifecycleLabels.For(to),
            remove_labels = current is null ? null : LifecycleLabels.For(current.Value)
        });

        using var resp = await httpClient.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            logger.LogWarning("GitLab label update failed: {Status} {Body}", resp.StatusCode, body);
            return TransitionResult.Failed($"HTTP {(int)resp.StatusCode}");
        }
        return TransitionResult.Succeeded();
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
}
