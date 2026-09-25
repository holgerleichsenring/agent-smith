using System.Net.Http;
using System.Text.Json;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// 2026-09-25-8e51e: rewrites the framework's region of a GitLab issue description.
/// <para>
/// GitLab keeps an issue description as the markdown it was given and returns the same bytes, so
/// the marker pair the filing wrote is found where it was written and everything outside it
/// survives unchanged — the round trip Jira does not have.
/// </para>
/// </summary>
public sealed class GitLabTicketRewriter(
    GitLabTicketConnection connection, HttpClient httpClient, ILogger logger) : ITicketRewriter
{
    private readonly TicketProviderHttpClient _http =
        TicketProviderHttpClient.WithPrivateToken(httpClient, connection.PrivateToken);

    public async Task<TicketRewriteResult> RewriteRegionAsync(
        TicketId ticketId, string region, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticketId);
        var url = $"{connection.BaseUrl.TrimEnd('/')}/api/v4/projects/{connection.ProjectPath}"
            + $"/issues/{ticketId.Value}";
        try
        {
            using var doc = await _http.SendForJsonAsync(HttpMethod.Get, url, null, cancellationToken);
            if (doc is null) return TicketRewriteResult.Failed($"GitLab has no issue {ticketId.Value}.");
            if (FramedTicketRegion.Replace(Description(doc), region) is not { } rewritten)
                return TicketRewriteResult.Unsupported(TicketRegionRefusal.NoRegion);
            logger.LogInformation(
                "TICKET WRITE #{Ticket}: issue description region <- {Caller}",
                ticketId.Value, TicketWriteAudit.Caller());
            await _http.SendAsync(HttpMethod.Put, url, new { description = rewritten }, cancellationToken);
            return TicketRewriteResult.Ok;
        }
        // The conversation that asked for the amendment is told; a thrown turn tells it nothing.
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Rewriting the region of GitLab issue {Ticket} failed", ticketId.Value);
            return TicketRewriteResult.Failed(ex.Message.Split('\n', 2)[0].Trim());
        }
    }

    private static string? Description(JsonDocument doc) =>
        doc.RootElement.TryGetProperty("description", out var description)
        && description.ValueKind == JsonValueKind.String
            ? description.GetString()
            : null;
}
