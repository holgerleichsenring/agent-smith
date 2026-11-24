using System.Security.Claims;
using AgentSmith.Application.Services.Pocs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Pocs.Variables;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Services.Pocs.Import;

/// <summary>
/// 2026-11-23-933db: Start of an admitted upload, all on the server: the caller's upload (read with THEIR key, so another
/// person's id answers an empty 404), the PoC it goes to (provisioning the caller's poc- project at the first import),
/// the Poc row and an admitted PocRevision — so the PoC has its id before any run — then the run gate and the poc-deploy
/// queue. A retried Start of the same upload reuses its rows: an upload whose run exists is answered as is, one whose
/// bytes were purged is refused 410 before anything is written. Imports are serialised within the process.
/// 2026-11-23-933dg: variables posted with Start are checked before anything is written, then written by a poc-variables
/// run in the PoC's project (claimed through the run gate, awaited) before poc-deploy is queued with that run handed
/// over; a failed write answers its line and queues nothing, so the same upload can be started again.
/// </summary>
internal sealed class PocImportLauncher(
    TicketProjects projects, PocUploadStore uploads, PocArchiveReader reader, PocImportRepository imports, PocRepository pocs,
    PocImportChoice choice, PocRunGate gate, PocVariableLauncher variables)
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
                return earlier.UploadSession == session ? await RetryAsync(user, earlier, session, request, ct) : (null, PocRefusal.NotFound);
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
        if (await variables.CheckAsync(null, Entries(request), ct) is { } unwritable) return (null, unwritable);
        var (poc, refused) = request.Choice?.Trim().ToLowerInvariant() switch
        {
            "replace" => await choice.ReplaceAsync(user, request, target, admitted, config, ct),
            "new" => await choice.NewAsync(user, request, target, config, ct),
            _ => (null, PocImportRefusals.BadChoice),
        };
        if (poc is null) return (null, refused);
        var actor = SpecDialogOwnership.PrincipalOf(user);
        return await QueueAsync(poc, await imports.AddRevisionAsync(poc.Id, session, uploadId, actor, ct), actor, true, request, ct);
    }

    private async Task<(PocImportAnswer?, PocRefusal?)> RetryAsync(
        ClaimsPrincipal user, PocRevision earlier, string session, PocImportRequest request, CancellationToken ct)
    {
        if (await uploads.ReadAsync(session, earlier.UploadId, ct) is null) return (null, PocImportRefusals.Purged(earlier.Number, projects.Config().Pocs.BlockedRetentionDays));
        if (await pocs.FindAsync(earlier.PocId, ct) is not { } poc) return (null, PocRefusal.NotFound);
        if (earlier.RunId is { } runId) return (new PocImportAnswer(poc.Id, earlier.Number, runId), null);
        return await QueueAsync(poc, earlier, SpecDialogOwnership.PrincipalOf(user), false, request, ct);
    }

    // A claim this request lost deletes the revision it wrote, so no number is burned; a retried revision stays. A
    // failed variables run keeps the revision unclaimed, so a retry of the same upload writes them and queues then.
    private async Task<(PocImportAnswer?, PocRefusal?)> QueueAsync(
        Poc poc, PocRevision revision, string actor, bool written, PocImportRequest request, CancellationToken ct)
    {
        string? handover = null;
        if (Entries(request) is { Count: > 0 } entries)
        {
            var (wrote, refused) = await variables.RunAsync(poc, entries, actor, ct);
            if (wrote is null) return (null, refused);
            handover = wrote.RunId;
        }
        var (runId, heldBy) = await gate.QueueDeployAsync(poc, revision, actor, ct, handover);
        if (runId is not null) return (new PocImportAnswer(poc.Id, revision.Number, runId), null);
        if (written) await imports.DeleteUnclaimedRevisionAsync(revision.Id, ct);
        return (null, PocImportRefusals.InProgress(poc, heldBy));
    }

    private static IReadOnlyList<AgentSmith.Contracts.Pocs.PocVariableEntry> Entries(PocImportRequest request) =>
        PocVariableEntries.Of(request.Variables, null);
}
