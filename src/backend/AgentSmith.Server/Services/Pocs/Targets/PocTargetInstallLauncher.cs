using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Services.Acts;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Services.Pocs.Targets;

/// <summary>
/// 2026-11-23-933dd: the Install button of a PoC target starts a poc-target-install run on the installation through
/// <see cref="IActRun"/>, like the config run — the server writes nothing itself — and answers how the run ended. A
/// target the configuration does not hold answers null.
/// </summary>
internal sealed class PocTargetInstallLauncher(IActRun runs, TicketProjects projects, TimeProvider clock)
{
    public async Task<PocTargetInstallAnswer?> InstallAsync(string target, CancellationToken ct)
    {
        if (projects.Config().Pocs.Targets.FirstOrDefault(t => string.Equals(t.Name, target, StringComparison.OrdinalIgnoreCase)) is not { } found)
            return null;
        var runId = AgentSmith.Application.Services.RunIdGenerator.Generate(clock.GetUtcNow());
        ct.ThrowIfCancellationRequested();
        var result = await runs.RunAsync(InstallationScope.Name, PipelinePresets.PocTargetInstallName,
            new Dictionary<string, object> { [ContextKeys.PocTargetName] = found.Name }, runId: runId);
        return new PocTargetInstallAnswer(found.Name, result.IsSuccess, result.Message, runId);
    }
}
