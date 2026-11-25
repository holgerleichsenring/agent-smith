using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Pocs;
using AgentSmith.Application.Services.Pocs.Build;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Exceptions;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-23-933dd: the last step of a poc-deploy run — the source repo host's pipeline builds the committed revision,
/// scans the image with Trivy and pushes it; agent smith starts it, follows it and reads the outcome back
/// (<see cref="PocBuildRun"/>). A revision built earlier keeps its digest; one blocked at the image stage stays blocked
/// unless the run is an overrule, which lifts its vulnerability blocks and builds afresh.
/// </summary>
public sealed class PocPipelineBuildHandler(
    IPocScanStore store, PocsConfigSource config, PocBuildOverrule overrule, PocBuildRun build,
    ILogger<PocPipelineBuildHandler> logger) : ICommandHandler<PocPipelineBuildContext>
{
    public async Task<CommandResult> ExecuteAsync(PocPipelineBuildContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Seeds is not { PocId: { } pocId, Revision: { } revision }
            || await store.ReadAsync(pocId, revision, cancellationToken) is not { } subject)
            return CommandResult.Fail("A poc-deploy run's build needs the PoC and revision it was seeded with; none was found.");
        if (PocBuildSettled.Of(subject, context.Seeds.Overrule, context.Pipeline) is { } settled) return settled;
        if (subject.BlockedStage == PocScanStages.Image)
        {
            var (applied, refused) = await overrule.ApplyAsync(subject, context.Seeds.OverruleIds, cancellationToken);
            if (applied is null) return CommandResult.Fail(refused!);
            subject = applied;
        }
        return await BuildAsync(context, subject, cancellationToken);
    }

    private async Task<CommandResult> BuildAsync(PocPipelineBuildContext context, PocScanSubject subject, CancellationToken ct)
    {
        var pocs = config.Current();
        if (pocs.Targets.FirstOrDefault(t => string.Equals(t.Name, subject.Target, StringComparison.OrdinalIgnoreCase)) is not { } target)
            return CommandResult.Fail($"'{subject.PocName}' names the target '{subject.Target}', which pocs.targets no longer holds.");
        if (subject.CommitSha is not { } sha) return CommandResult.Fail($"Revision {subject.Revision} of '{subject.PocName}' is not committed; nothing is built.");
        if (context.Project.Repos.FirstOrDefault() is not { } projectRepo)
            return CommandResult.Fail($"The project '{context.Project.Name}' names no source repo to build '{subject.PocName}' from.");
        var repo = PocMachine.Stamp(pocs, projectRepo); // 2026-11-25-0873
        try
        {
            return await build.RunAsync(new PocBuildJob(context, subject, target, pocs.Scan, repo, sha), ct);
        }
        catch (Exception ex) when (ex is ContainerRegistryException or HttpRequestException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Building revision {Revision} of PoC {PocId} failed", subject.Revision, subject.PocId);
            return CommandResult.Fail($"Revision {subject.Revision} of '{subject.PocName}' was not built: {ex.Message}");
        }
    }
}
