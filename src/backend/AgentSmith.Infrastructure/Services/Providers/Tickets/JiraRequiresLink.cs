using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// 2026-10-14-dbcem: a Blocks issue link on Jira — the required ticket blocks the dependent. The path follows the
/// configured issue collection (<c>/rest/api/3/issue</c> becomes <c>/rest/api/3/issueLink</c>, a Data Center's
/// <c>/rest/api/2/issue</c> its <c>/rest/api/2/issueLink</c>), so no endpoint of its own is configured.
/// </summary>
public sealed class JiraRequiresLink(JiraTicketConnection connection, HttpClient httpClient, ILogger logger) : ITicketRequiresLink
{
    private readonly TicketProviderHttpClient _http =
        TicketProviderHttpClient.WithBasicAuth(httpClient, connection.Email, connection.ApiToken);

    public async Task<RequiresLinkResult> LinkAsync(TicketId dependent, TicketId required, CancellationToken cancellationToken)
    {
        var url = $"{connection.BaseUrl.TrimEnd('/')}{connection.ResolvedEndpoints.Create.TrimEnd('/')}Link";
        try
        {
            await _http.SendAsync(HttpMethod.Post, url, Body(required.Value, dependent.Value), cancellationToken);
            return RequiresLinkResult.Done;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Jira issue {Dependent} could not be linked to the issue {Required} it requires", dependent.Value, required.Value);
            return RequiresLinkRefusal.Of(ex);
        }
    }

    // The ONE place the direction lives: the inward issue is the one that blocks, read back as "<required> blocks <dependent>".
    internal static object Body(string required, string dependent) => new
    {
        type = new { name = "Blocks" },
        inwardIssue = new { key = required },
        outwardIssue = new { key = dependent },
    };
}
