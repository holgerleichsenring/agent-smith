using System.Security.Claims;
using AgentSmith.Application.Services.Pocs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Services.Pocs.Import;

/// <summary>
/// 2026-11-23-933db: Start of an admitted upload, all on the server: the caller's upload (read with THEIR key, so another
/// person's id answers an empty 404), the PoC it goes to (provisioning the caller's poc- project at the first import),
/// the Poc row and an admitted PocRevision — so the PoC has its id before any run — then the run gate and the poc-deploy
/// queue. A retried Start of the same upload reuses its rows: an upload whose run exists is answered as is, one whose
/// bytes were purged is refused 410 before anything is written. Imports are serialised within the process.
/// </summary>
internal sealed class PocImportLauncher(
    TicketProjects projects, PocUploadStore uploads, PocArchiveReader reader, PocImportRepository imports, PocRepository pocs,
    PocImportChoice choice, PocRunGate gate)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<(PocImportAnswer? Answer, PocRefusal? Refusal)> ImportAsync(
        ClaimsPrincipal user, string uploadId, PocImportRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        await Gate.WaitAsync(ct);
        try
        {
            var session = PocUploadKey.For(user);
            if (await imports.RevisionOfUploadAsync(uploadId, ct) is { } earlier)
                return earlier.UploadSession == session ? await RetryAsync(user, earlier, session, ct) : (null, PocRefusal.NotFound);
            return await uploads.ReadAsync(session, uploadId, ct) is { } upload
                ? await FirstAsync(user, session, uploadId, upload.Archive, request, ct)
                : (null, PocRefusal.NotFound);
        }
        finally { Gate.Release(); }
    }

    private async Task<(PocImportAnswer?, PocRefusal?)> FirstAsync(
        ClaimsPrincipal user, string session, string uploadId, byte[] archive, PocImportRequest request, CancellationToken ct)
    {
        var config = projects.Config().Pocs;
        var (target, why) = PocTargetChoice.Of(config, request.Target);
        if (target is null) return (null, PocRefusal.BadRequest(why!));
        var admitted = reader.Admit(archive, config.Archive);
        if (admitted.Refusal is { } refusal) return (null, new PocRefusal(StatusCodes.Status422UnprocessableEntity, refusal));
        var (poc, refused) = request.Choice?.Trim().ToLowerInvariant() switch
        {
            "replace" => await choice.ReplaceAsync(user, request, target, admitted, config, ct),
            "new" => await choice.NewAsync(user, request, target, config, ct),
            _ => (null, PocImportRefusals.BadChoice),
        };
        if (poc is null) return (null, refused);
        var actor = SpecDialogOwnership.PrincipalOf(user);
        return await QueueAsync(poc, await imports.AddRevisionAsync(poc.Id, session, uploadId, actor, ct), actor, true, ct);
    }

    private async Task<(PocImportAnswer?, PocRefusal?)> RetryAsync(
        ClaimsPrincipal user, PocRevision earlier, string session, CancellationToken ct)
    {
        if (await uploads.ReadAsync(session, earlier.UploadId, ct) is null) return (null, PocImportRefusals.Purged(earlier.Number, projects.Config().Pocs.BlockedRetentionDays));
        if (await pocs.FindAsync(earlier.PocId, ct) is not { } poc) return (null, PocRefusal.NotFound);
        if (earlier.RunId is { } runId) return (new PocImportAnswer(poc.Id, earlier.Number, runId), null);
        return await QueueAsync(poc, earlier, SpecDialogOwnership.PrincipalOf(user), false, ct);
    }

    // A claim this request lost deletes the revision it wrote, so no number is burned; a retried revision stays.
    private async Task<(PocImportAnswer?, PocRefusal?)> QueueAsync(
        Poc poc, PocRevision revision, string actor, bool written, CancellationToken ct)
    {
        var (runId, heldBy) = await gate.QueueDeployAsync(poc, revision, actor, ct);
        if (runId is not null) return (new PocImportAnswer(poc.Id, revision.Number, runId), null);
        if (written) await imports.DeleteUnclaimedRevisionAsync(revision.Id, ct);
        return (null, PocImportRefusals.InProgress(poc, heldBy));
    }
}
