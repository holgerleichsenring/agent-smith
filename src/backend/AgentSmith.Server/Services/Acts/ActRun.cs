using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Acts;

/// <summary>
/// 2026-11-17-e061a: runs an act's preset through the full run lifecycle (ExecutePipelineUseCase), headless and with
/// no ticket id — an act holds no ticket lease and finalizes no ticket. It runs on no token: the act was accepted, and
/// one a caller's cancellation broke off halfway would leave a host changed and nothing recorded. Extracted from
/// 5701b's FilingRun, which now runs through it.
/// </summary>
public sealed class ActRun(IServiceProvider services, ServerContext server, ILogger<ActRun> logger) : IActRun
{
    public async Task<CommandResult> RunAsync(
        string project, string pipeline, IReadOnlyDictionary<string, object> seeds, TicketId? shownOnTicket = null)
    {
        ArgumentNullException.ThrowIfNull(seeds);
        var request = new PipelineRequest(
            ProjectName: project, PipelineName: pipeline, Headless: true, Context: new Dictionary<string, object>(seeds),
            ShownOnTicket: shownOnTicket);
        try
        {
            // Resolved when an act runs: the run lifecycle reads the configuration file, which a composition that
            // merely holds an act (a turn, a nudge) must not need to construct.
            var pipelines = services.GetRequiredService<ExecutePipelineUseCase>();
            return await pipelines.ExecuteAsync(request, server.ConfigPath, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The '{Pipeline}' run of project {Project} failed", pipeline, project);
            return CommandResult.Fail(ex.Message, ex);
        }
    }
}
