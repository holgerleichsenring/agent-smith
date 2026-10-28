using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// 2026-10-28-7a69b: rewrites the framework's region of a Jira description inside the stored
/// document.
/// <para>
/// 2026-09-25-8e51e refused this, because reading a description as text drops mentions, emoji,
/// media, inline cards and status nodes and flattens marks, headings, lists and tables. The refusal
/// was about the TEXT round trip, not about Jira: the description is read as its document,
/// <see cref="JiraAdfRegion"/> swaps the region's paragraphs, and every other node is sent back as
/// it came. A description that is not a document — a Data Center answering on REST v2 with wiki
/// text — is still refused, with the reason.
/// </para>
/// </summary>
public sealed class JiraTicketRewriter(JiraTicketConnection connection, HttpClient httpClient, ILogger logger) : ITicketRewriter
{
    /// <summary>What an operator is told when the description comes back as text.</summary>
    public const string NotADocument =
        "Jira returned this description as text rather than as a document (a Data Center answering "
        + "on REST v2), and rewriting text would flatten what a person wrote. Paste the part you want "
        + "into the ticket yourself; the approved specification is stored and on the ticket branch.";

    private readonly TicketProviderHttpClient _http =
        TicketProviderHttpClient.WithBasicAuth(httpClient, connection.Email, connection.ApiToken);

    public async Task<TicketRewriteResult> RewriteRegionAsync(
        TicketId ticketId, string region, RegionPlacement placement, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticketId);
        var url = $"{connection.BaseUrl.TrimEnd('/')}{connection.ResolvedEndpoints.IssueFor(ticketId.Value)}";
        try
        {
            using var doc = await _http.SendForJsonAsync(HttpMethod.Get, $"{url}?fields=description", null, cancellationToken);
            if (doc is null) return TicketRewriteResult.Failed($"Jira has no issue {ticketId.Value}.");
            if (!TryReadDocument(doc, out var description)) return TicketRewriteResult.Unsupported(NotADocument);
            if (JiraAdfRegion.Place(description, region, placement == RegionPlacement.ReplaceOrAppend) is not { } written)
                return TicketRewriteResult.Unsupported(TicketRegionRefusal.NoRegion);
            logger.LogInformation("TICKET WRITE #{Ticket}: issue description region <- {Caller}",
                ticketId.Value, TicketWriteAudit.Caller());
            await _http.SendAsync(HttpMethod.Put, url, new { fields = new { description = written } }, cancellationToken);
            return TicketRewriteResult.Ok;
        }
        // The conversation that asked for the amendment is told; a thrown turn tells it nothing.
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Rewriting the region of Jira issue {Ticket} failed", ticketId.Value);
            return TicketRewriteResult.Failed(ex.Message.Split('\n', 2)[0].Trim());
        }
    }

    // An absent or null description is an empty document; a string is wiki text, which is no document.
    private static bool TryReadDocument(JsonDocument doc, out JsonObject? description)
    {
        description = null;
        if (!doc.RootElement.TryGetProperty("fields", out var fields)
            || !fields.TryGetProperty("description", out var held) || held.ValueKind == JsonValueKind.Null)
            return true;
        description = held.ValueKind == JsonValueKind.Object ? JsonNode.Parse(held.GetRawText()) as JsonObject : null;
        return description is not null;
    }
}
