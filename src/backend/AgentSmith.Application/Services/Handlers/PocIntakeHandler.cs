using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Pocs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-23-933db: the step of a poc-deploy run, in process and with no sandbox. It reads the Poc and PocRevision rows
/// the seeds name (it never creates a Poc row), reads the stored archive, checks it by every archive and Dockerfile rule
/// on the inflated data and records the revision validated or refused. A revision already evaluated is not evaluated
/// again (2026-11-23-933dc: blocked and overruled ones included). The code scan and the commit follow.
/// </summary>
public sealed class PocIntakeHandler(
    IPocIntakeStore store, PocArchiveReader reader, PocArchiveLimitsSource limits, PocImportedPublisher published,
    ILogger<PocIntakeHandler> logger) : ICommandHandler<PocIntakeContext>
{
    public async Task<CommandResult> ExecuteAsync(PocIntakeContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.PocId is not { } pocId || context.Revision is not { } revision)
            return CommandResult.Fail($"A poc-deploy run needs the PoC and revision the server seeds under ContextKeys.{nameof(ContextKeys.PocId)} "
                + $"and ContextKeys.{nameof(ContextKeys.PocRevision)}. Nothing was written.");
        if (await store.ReadAsync(pocId, revision, cancellationToken) is not { } subject)
            return CommandResult.Fail($"No PoC {pocId} with a revision {revision} exists; the import writes both before this run. Nothing was written.");
        if (PocRevisionStates.IsEvaluated(subject.RevisionState)) // 933dc: blocked and overruled too
            return CommandResult.Ok($"Revision {revision} of '{subject.PocName}' was already {subject.RevisionState}; it is not checked again.");
        if (await store.ArchiveAsync(subject.UploadSession, subject.UploadId, cancellationToken) is not { } archive)
            return CommandResult.Fail($"The upload of revision {revision} of '{subject.PocName}' is gone; upload the ZIP again.");
        return await RecordAsync(context, subject, reader.Intake(archive, limits.Current()), cancellationToken);
    }

    private async Task<CommandResult> RecordAsync(
        PocIntakeContext context, PocIntakeSubject subject, PocArchiveIntake intake, CancellationToken ct)
    {
        var runId = context.Pipeline.TryGet<string>(ContextKeys.RunId, out var id) ? id ?? string.Empty : string.Empty;
        if (intake.Refusal is { } refusal)
        {
            await store.RecordAsync(new PocIntakeOutcome(subject.PocId, subject.Revision, runId, PocRevisionStates.Refused,
                null, null, null, null, refusal), ct);
            logger.LogInformation("Revision {Revision} of PoC {PocId} refused: {Refusal}", subject.Revision, subject.PocId, refusal);
            return CommandResult.Fail($"Revision {subject.Revision} of '{subject.PocName}' was refused: {refusal}");
        }

        var dockerfile = intake.Dockerfile!;
        await store.RecordAsync(new PocIntakeOutcome(subject.PocId, subject.Revision, runId, PocRevisionStates.Validated,
            intake.Manifest!.ToJson(), dockerfile.Port, dockerfile.Entrypoint, dockerfile.BaseImage, null), ct);
        await published.PublishAsync(context.Pipeline, subject.PocId, subject.Revision, context.Actor, ct);
        return CommandResult.Ok($"Revision {subject.Revision} of '{subject.PocName}' passed every check: {intake.Manifest.Files.Count} files, "
            + $"port {dockerfile.Port}. The code scan comes next.");
    }
}
