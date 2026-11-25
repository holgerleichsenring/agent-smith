using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Pocs;
using AgentSmith.Application.Services.Pocs.Commit;
using AgentSmith.Application.Services.Pocs.Scan;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-23-933dc: commits a revision that passed the code scan (or was overruled) into its PoC's folder of the
/// target's source repo, on the default branch, and publishes the sha under ContextKeys.PocSourceCommitSha. A revision
/// that is not the PoC's latest is never written (a retried old upload would put old files back); a revision already
/// committed reuses its sha without a clone.
/// </summary>
public sealed class PocSourceCommitHandler(
    IPocScanStore store, PocRevisionFiles revisionFiles, PocFolderWrite folders, PocsConfigSource config, ILogger<PocSourceCommitHandler> logger)
    : ICommandHandler<PocSourceCommitContext>
{
    public async Task<CommandResult> ExecuteAsync(PocSourceCommitContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Seeds is not { PocId: { } pocId, Revision: { } revision }
            || await store.ReadAsync(pocId, revision, cancellationToken) is not { } subject)
            return CommandResult.Fail("A poc-deploy run's commit needs the PoC and revision it was seeded with; none was found.");
        if (!PassedCodeScan(subject) || subject.VerdictJson is null)
            return CommandResult.Fail($"Revision {revision} of '{subject.PocName}' did not pass the code scan; nothing is committed.");
        if (!subject.IsLatest)
            return CommandResult.Fail($"Revision {revision} is older than r{subject.LatestRevision} of '{subject.PocName}'; nothing is committed.");
        if (subject.CommitSha is { } committed) return Published(context, committed, $"Revision {revision} was committed earlier as {committed}.");
        if (context.Project.Repos.FirstOrDefault() is not { } repo)
            return CommandResult.Fail($"The project '{context.Project.Name}' names no source repo to commit '{subject.PocName}' to.");
        return await CommitAsync(context, subject, PocMachine.Stamp(config.Current(), repo), cancellationToken); // 2026-11-25-0873
    }

    private async Task<CommandResult> CommitAsync(
        PocSourceCommitContext context, PocScanSubject subject, AgentSmith.Contracts.Models.Configuration.RepoConnection repo, CancellationToken ct)
    {
        var (files, unreadable) = await revisionFiles.ReadAsync(subject, ct);
        if (unreadable is not null) return CommandResult.Fail($"Nothing was committed: {unreadable}.");
        var message = $"poc {subject.PocName} r{subject.Revision}\n\nCo-authored-by: {context.Seeds.Actor ?? "unknown"}";
        try
        {
            var sha = await folders.WriteAsync(context.Project, repo, subject.Folder, files, message, ct);
            await store.RecordCommitAsync(subject.PocId, subject.Revision, sha, ct);
            return Published(context, sha, $"Revision {subject.Revision} of '{subject.PocName}' is commit {sha} in {repo.Name}:{subject.Folder}.");
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Committing revision {Revision} of PoC {PocId} failed", subject.Revision, subject.PocId);
            return CommandResult.Fail($"Revision {subject.Revision} of '{subject.PocName}' could not be committed to {repo.Name}: {ex.Message}");
        }
    }

    // 2026-11-23-933dd: built, or blocked only at the image stage, it passed the code scan and was committed before.
    private static bool PassedCodeScan(PocScanSubject subject) =>
        subject.RevisionState is PocRevisionStates.Validated or PocRevisionStates.Overruled or PocRevisionStates.Built
        || (subject.RevisionState == PocRevisionStates.Blocked && subject.BlockedStage == PocScanStages.Image);

    private static CommandResult Published(PocSourceCommitContext context, string sha, string message)
    {
        context.Pipeline.Set(ContextKeys.PocSourceCommitSha, sha);
        return CommandResult.Ok(message);
    }
}
