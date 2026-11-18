using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-11-17-3435b: runs one merge-green run under the ticket's lease, taken here as a derivation's is — the run carries
/// the ticket, so it is one of the ticket's runs, and the lease keeps a rework off the ticket while it merges; the run
/// leaves the ticket's status alone (LeavesTicketAlone). It runs on no token: a merge broken off halfway would leave a
/// host changed and nothing recorded. Answers whether it ran; a lease another run holds is a no.
/// </summary>
public sealed class MergeGreenRun(
    IServiceProvider services, ServerContext server, IActiveRunLease leases, ILogger<MergeGreenRun> logger) : IMergeGreenRun
{
    public async Task<bool> RunAsync(ResolvedProject project, CiGreenMergeRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(request);
        var ticket = new TicketId(request.TicketId);
        if (await leases.TryClaimAsync(project.Name, ticket, ct) != LeaseClaimOutcome.Claimed) return false;
        try
        {
            var result = await services.GetRequiredService<ExecutePipelineUseCase>().ExecuteAsync(
                new PipelineRequest(project.Name, PipelinePresets.MergeGreenName, TicketId: ticket, Headless: true,
                    Context: new Dictionary<string, object> { [ContextKeys.CiGreenMerge] = request }),
                server.ConfigPath, CancellationToken.None);
            logger.LogInformation("CI green on {Project}/#{Ticket}: merge-green {Outcome}: {Message}",
                project.Name, request.TicketId, result.IsSuccess ? "ran" : "failed", result.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The merge-green run of {Project}/#{Ticket} failed to start", project.Name, request.TicketId);
            await leases.ReleaseAsync(project.Name, ticket, runId: null, CancellationToken.None);
        }
        return true;
    }
}
