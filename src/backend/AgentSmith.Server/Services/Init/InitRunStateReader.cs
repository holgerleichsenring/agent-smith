using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;

namespace AgentSmith.Server.Services.Init;

/// <summary>
/// 2026-10-02-5f89d: reads a project's live init run so the Initialize button can restore
/// itself after navigation instead of forgetting a run it started. The route's name is
/// resolved to the configured project exactly as <see cref="InitRunLauncher"/> resolves it,
/// and the row is read under the configured spelling the launch writes — so "Sample" and
/// "sample" read the same run, the same rule the double-start guard follows.
/// </summary>
public sealed class InitRunStateReader(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    InitRunRepository runs)
{
    public async Task<InitRunStateLookup> ReadAsync(string projectName, CancellationToken ct)
    {
        var config = configLoader.LoadConfig(serverContext.ConfigPath);
        if (!config.Projects.TryGetValue(projectName, out var project))
            return InitRunStateLookup.UnknownProject;

        var live = await runs.FindLiveRunAsync(project.Name, InitRunLauncher.PipelineName, ct);
        return new InitRunStateLookup(true, live is null ? null : Map(live));
    }

    private static InitRunStateResponse Map(Run run) => new(run.Id, run switch
    {
        { CancelRequested: true } => InitRunStateResponse.Cancelling,
        { Status: RunStatuses.Queued } => InitRunStateResponse.Queued,
        _ => InitRunStateResponse.Running,
    });
}
