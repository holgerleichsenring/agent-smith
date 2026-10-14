using AgentSmith.Contracts.Commands;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.ChatLaunch;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-10-08-e8b9b: the rework attempt starts through ChatTicketRunLauncher — the funnel, the
/// capacity queue, the claim — like a ticket a person named. The launcher is scoped, this is a
/// singleton beside the webhook handlers, so it is reached through a scope of its own. A ticket
/// another claimer took in the meantime counts as started: that claimer serves the act.
/// </summary>
public sealed class ReworkLaunch(IServiceScopeFactory scopes) : IReworkLaunch
{
    public Task<ReworkOutcome> LaunchAsync(string project, string ticketId, string pipeline, CancellationToken cancellationToken) =>
        StartAsync(project, ticketId, pipeline, cancellationToken, seriesAutoStart: false);

    /// <summary>2026-10-13-8762v: the series' next attempt — the same funnel, marked so its cause reads series.</summary>
    public Task<ReworkOutcome> StartSeriesAttemptAsync(string project, string ticketId, CancellationToken cancellationToken) =>
        StartAsync(project, ticketId, PipelinePresets.CodeName, cancellationToken, seriesAutoStart: true);

    private async Task<ReworkOutcome> StartAsync(
        string project, string ticketId, string pipeline, CancellationToken cancellationToken, bool seriesAutoStart)
    {
        using var scope = scopes.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ChatTicketRunLauncher>()
            .LaunchAsync(project, ticketId, pipeline, cancellationToken, seriesAutoStart);
        if (result.RunId is not null || result.TakenByAnother) return ReworkOutcome.Started(result.RunId);
        return ReworkOutcome.Refused(result.Refusal ?? "the run could not be started");
    }
}
