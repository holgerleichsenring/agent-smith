using System.Security.Claims;
using AgentSmith.Application.Services.Pocs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Services.Pocs.Import;

/// <summary>
/// 2026-11-23-933db: admits one PoC ZIP. It reads the central directory (and the root Dockerfile, inflated with a
/// counted bound) and runs every rule declared data can answer — the binary bound needs inflated content and runs in the
/// poc-deploy run. An admitted archive is stored under the caller's key, replacing their un-imported uploads, and the
/// answer carries the name proposal and the comparison with the caller's own live PoC of that name on the target.
/// </summary>
internal sealed class PocUploadAdmission(
    TicketProjects projects, PocArchiveReader reader, PocUploadStore store, PocImportRepository imports,
    PocIdentity identity, PocActAccess access)
{
    public async Task<(PocUploadAnswer? Answer, PocRefusal? Refusal)> AdmitAsync(
        ClaimsPrincipal user, PocUpload upload, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(upload);
        var pocs = projects.Config().Pocs;
        if (upload.Archive.Length == 0) return (null, PocRefusal.BadRequest("Send one ZIP file in the form field 'file'."));
        var (target, why) = PocTargetChoice.Of(pocs, upload.Target);
        if (target is null) return (null, PocRefusal.BadRequest(why!));
        var admitted = reader.Admit(upload.Archive, pocs.Archive);
        if (admitted.Refusal is { } refusal)
            return (null, new PocRefusal(StatusCodes.Status422UnprocessableEntity, refusal));
        var uploadId = await store.ReplaceAsync(PocUploadKey.For(user), upload.FileName, upload.Archive, ct);
        var proposal = PocNameProposal.From(upload.FileName);
        return (new PocUploadAnswer(uploadId, upload.FileName, target.Name, admitted.Dockerfile!.Port, admitted.Entries.Count,
            admitted.UnpackedBytes, proposal, PocArchiveChecks.Admission,
            await MatchAsync(user, target.Name, proposal, admitted, pocs.Archive, ct)), null);
    }

    private async Task<PocMatchView?> MatchAsync(
        ClaimsPrincipal user, string target, string name, PocArchiveAdmission admitted, PocArchiveLimits limits, CancellationToken ct)
    {
        var poc = await imports.HolderOfAsync(target, name, ct);
        if (poc is null || !PocStates.IsLive(poc.State) || !access.MayAct(user, poc)) return null;
        var answer = await identity.CompareAsync(poc.Id, admitted, limits, ct);
        var m = answer.Match;
        return new PocMatchView(poc.Id, poc.Name, answer.Revision, m.Added, m.Changed, m.Removed, m.Unchanged, m.RatioPercent,
            m.PortChanged, m.EntrypointChanged, answer.PreviousPort, m.Verdict);
    }
}
