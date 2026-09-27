using System.Net;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Tickets;
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
    GitLabTicketConnection connection,
    HttpClient httpClient,
    ILogger<GitLabTicketStatusTransitioner> logger) : ITicketStatusTransitioner
{
    private readonly string _baseUrl = connection.BaseUrl.TrimEnd('/');
    private readonly string _projectPath = connection.ProjectPath;
    private readonly string _privateToken = connection.PrivateToken;
    private readonly TicketLabelVocabulary _labels = connection.ResolvedLabels;

    public string ProviderType => "GitLab";

    public async Task<TicketLifecycleStatus?> ReadCurrentAsync(
        TicketId ticketId, CancellationToken cancellationToken)
    {
        using var scope = logger.BeginScope("ticket={Ticket}", ticketId.Value);
        var labels = await FetchLabelsAsync(ticketId, cancellationToken);
        return labels is null ? null : ParseLifecycle(labels);
    }

    public async Task<TransitionResult> TransitionAsync(
        TicketId ticketId, TicketLifecycleStatus from,
        TicketLifecycleStatus to, CancellationToken cancellationToken)
    {
        using var scope = logger.BeginScope("ticket={Ticket}", ticketId.Value);
        logger.LogInformation(
            "GitLab Transition #{Ticket}: {From} → {To}", ticketId.Value, from, to);

        var labels = await FetchLabelsAsync(ticketId, cancellationToken);
        if (labels is null)
        {
            logger.LogWarning("GitLab Transition #{Ticket}: ticket not found", ticketId.Value);
            return TransitionResult.NotFound();
        }

        // p0262: lifecycle tags are pure markers — set `to` unconditionally, no `from`
        // precondition; `from` is advisory. Run-level single-run is the lease's job (p0246b).
        // 2026-09-25-3c7ac: the LABELS travel, not the state read off them, so the strip can be a
        // predicate over what is actually on the ticket.
        var result = await UpdateLabelsAsync(ticketId, labels, to, cancellationToken);
        logger.LogInformation(
            "GitLab Transition #{Ticket}: {Outcome}", ticketId.Value, result.Outcome);
        return result;
    }

    private async Task<string[]?> FetchLabelsAsync(TicketId ticketId, CancellationToken ct)
    {
        var url = $"{_baseUrl}/api/v4/projects/{_projectPath}/issues/{ticketId.Value}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("PRIVATE-TOKEN", _privateToken);

        using var resp = await httpClient.SendAsync(req, ct);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("labels", out var labelsEl)) return [];
        return labelsEl.EnumerateArray().Select(e => e.GetString() ?? "").ToArray();
    }

    private async Task<TransitionResult> UpdateLabelsAsync(
        TicketId ticketId, string[] labels, TicketLifecycleStatus to, CancellationToken ct)
    {
        var url = $"{_baseUrl}/api/v4/projects/{_projectPath}/issues/{ticketId.Value}";
        using var req = new HttpRequestMessage(HttpMethod.Put, url);
        req.Headers.Add("PRIVATE-TOKEN", _privateToken);
        // 2026-09-25-3c7ac: removal by PREDICATE over the labels the ticket carries. Removing the
        // literal computed for the state we believe it is in reads the historical word and removes
        // the configured one, which is not there — a renamed board kept both, for ever.
        var target = _labels.For(to);
        var stale = string.Join(",", labels
            .Where(l => _labels.IsLifecycleLabel(l))
            .Where(l => !string.Equals(l, target, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase));
        req.Content = JsonContent.Create(new
        {
            add_labels = target,
            remove_labels = stale.Length == 0 ? null : stale
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


    private TicketLifecycleStatus? ParseLifecycle(string[] labels)
    {
        foreach (var label in labels)
            if (_labels.TryParse(label, out var status))
                return status;
        return null;
    }
}
