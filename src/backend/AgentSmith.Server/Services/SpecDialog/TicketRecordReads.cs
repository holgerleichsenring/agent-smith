using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Runs;
using AgentSmith.Server.Services.Admin.Read;
using AgentSmith.Server.Services.Tickets;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-18-d6d2b: the admin assistant's read_run_events and read_config lent to the ticket steward, as the series
/// master borrows them (SeriesRecordReads) — the same readers, masked against every configured credential and cut at
/// dialogue.admin_result_max_chars — scoped to the bound ticket: a run is read only when it is one of this ticket's
/// runs in a project sharing its tracker, and the configuration is the bound project's. Each read opens its own scope.
/// </summary>
public sealed class TicketRecordReads(
    IServiceScopeFactory scopes, SecretMasker masker, string project, string ticketId) : ITicketRecordReads
{
    public IList<AITool> Tools()
    {
        using var scope = scopes.CreateScope();
        var chars = scope.ServiceProvider.GetRequiredService<AdminReadSettings>().Chars;
        AIFunction[] reads =
        [
            AIFunctionFactory.Create(ReadRunEventsAsync, "read_run_events",
                "Reads one of THIS ticket's runs event by event in sequence order: seq, type, step, time, role and payload — "
                + "the commands a sandbox ran with their exit codes and output tails (type SandboxResult), each step's "
                + "result (StepCompleted), the tool results (ToolResult) and how the run ended (RunFinished). The run ids are "
                + "ticket_runs' ids; a run of another ticket is refused. Pass the last seq you saw as afterSeq for the next page."),
            AIFunctionFactory.Create(ReadConfig, "read_config",
                "Reads this ticket's project configuration as runs resolve it — repositories, tracker statuses (trigger, "
                + "done, failed), agent and every resolved setting with where it came from. It carries no secret value."),
        ];
        return [.. reads.Select(tool => new BoundedResultTool(new MaskedResultTool(tool, masker), chars))];
    }

    private async Task<string> ReadRunEventsAsync(string runId, string? type = null, int? step = null,
        long? afterSeq = null, int? limit = null, CancellationToken cancellationToken = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        if (!await IsTicketRunAsync(services, runId, cancellationToken))
            return $"Run '{runId}' is not one of ticket {ticketId}'s runs; read_run_events reads only those (see ticket_runs).";
        return await services.GetRequiredService<AdminEventTools>()
            .ReadRunEvents(runId, type, step, afterSeq, limit, cancellationToken);
    }

    private string ReadConfig()
    {
        using var scope = scopes.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AdminConfigReadTools>().ReadConfig(project);
    }

    // The ticket's runs in every project sharing its tracker: a sibling's attempt is this ticket's attempt too.
    private async Task<bool> IsTicketRunAsync(IServiceProvider services, string runId, CancellationToken ct)
    {
        var runs = services.GetRequiredService<TicketRunsQuery>();
        foreach (var sibling in services.GetRequiredService<FiledWorkTrackerProjects>().SharingTrackerWith(project))
            if ((await runs.ForTicketAsync(sibling.Name, ticketId, ct)).Any(r => r.Run.Id == runId))
                return true;
        return false;
    }
}
