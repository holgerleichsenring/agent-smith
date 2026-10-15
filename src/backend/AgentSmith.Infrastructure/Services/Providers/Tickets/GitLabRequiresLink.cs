using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// 2026-10-14-dbcem: a related issue on GitLab — relates_to is the one issue link every tier offers (blocks needs a
/// paid one). The URL carries the project path as configured, URL-encoded; the body takes it decoded.
/// </summary>
public sealed class GitLabRequiresLink(GitLabTicketConnection connection, HttpClient httpClient, ILogger logger) : ITicketRequiresLink
{
    private readonly TicketProviderHttpClient _http =
        TicketProviderHttpClient.WithPrivateToken(httpClient, connection.PrivateToken);

    public async Task<RequiresLinkResult> LinkAsync(TicketId dependent, TicketId required, CancellationToken cancellationToken)
    {
        var url = $"{connection.BaseUrl.TrimEnd('/')}/api/v4/projects/{connection.ProjectPath}/issues/{dependent.Value}/links";
        try
        {
            await _http.SendAsync(HttpMethod.Post, url, new
            {
                target_project_id = Uri.UnescapeDataString(connection.ProjectPath),
                target_issue_iid = required.Value,
                link_type = "relates_to",
            }, cancellationToken);
            return RequiresLinkResult.Done;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "GitLab issue {Dependent} could not be linked to the issue {Required} it requires", dependent.Value, required.Value);
            return RequiresLinkRefusal.Of(ex);
        }
    }
}
