using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// 2026-09-27-5c1ea: searches Azure DevOps work items by title and body for the dialog's ticket
/// picker. The clause comes from <see cref="AzureDevOpsTextMatchClause"/>, the scope from
/// <see cref="AzureDevOpsOpenScope"/> — the same prefix the discovery lister uses, so the two
/// agree on the team project and on what "open" means — and the run from
/// <see cref="AzureDevOpsWiqlRunner"/>, which throws so that this class can answer a failed query
/// as a refusal instead of as an empty board.
/// </summary>
public sealed class AzureDevOpsTicketSearch : ITicketSearch
{
    private readonly AzureDevOpsWiqlRunner _runner;
    private readonly string _project;
    private readonly IReadOnlyList<string>? _openStates;
    private readonly ILogger _logger;

    internal AzureDevOpsTicketSearch(
        AzureDevOpsWiqlRunner runner, string project, IReadOnlyList<string>? openStates, ILogger logger)
    {
        _runner = runner;
        _project = project;
        _openStates = openStates;
        _logger = logger;
    }

    public AzureDevOpsTicketSearch(
        AzureDevOpsTicketConnection connection, IReadOnlyList<string>? openStates,
        IReadOnlyList<string>? extraFields, ILogger logger)
        : this(
            new AzureDevOpsWiqlRunner(
                new AzureDevOpsConnectionCache(connection, logger), new AzureDevOpsFieldMapper(),
                connection.Project, extraFields, logger),
            connection.Project, openStates, logger)
    {
    }

    public async Task<TicketSearchResult> SearchAsync(
        string text, int limit, CancellationToken cancellationToken)
    {
        if (AzureDevOpsTextMatchClause.For(text) is not { } match) return TicketSearchResult.None;
        var where = $"{AzureDevOpsOpenScope.Where(_project, _openStates)} AND {match}";
        try
        {
            var tickets = await _runner.RunAsync(where, limit + 1, cancellationToken);
            return TicketSearchResult.Of(
                tickets.Select(t => new TicketSearchHit(t.Id, t.Title)), limit);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "AzDO ticket search failed for project={Project}", _project);
            return TicketSearchResult.Failed(ex.Message.Split('\n', 2)[0].Trim());
        }
    }
}
