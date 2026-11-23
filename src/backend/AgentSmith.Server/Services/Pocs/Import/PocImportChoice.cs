using System.Security.Claims;
using AgentSmith.Application.Services.Pocs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.SpecDialog;

namespace AgentSmith.Server.Services.Pocs.Import;

/// <summary>
/// 2026-11-23-933db: the PoC an import goes to. Replace: the caller's live PoC on the target (anyone else's answers an
/// empty 404), the different-app guard recomputed against IT and an 'ask' refused unless confirmed. New: a DNS-label
/// name of at most 40, free on the target unless the caller's own refused PoC holds it (then reused, back to importing),
/// within the per-person limit unless pocs.admin; the caller's poc- project is provisioned first. A live run of a PoC
/// that already exists is refused before anything is written.
/// </summary>
internal sealed class PocImportChoice(
    PocRepository pocs, PocImportRepository imports, PocIdentity identity, PocActAccess acts, ProjectAccess access,
    PocProjectProvisioner provisioner, PocRunGate gate)
{
    private static readonly string[] Admin = [Permissions.PocsAdmin];

    public async Task<(Poc? Poc, PocRefusal? Refusal)> ReplaceAsync(
        ClaimsPrincipal user, PocImportRequest request, PocTargetConfig target, PocArchiveAdmission admitted, PocsConfig config, CancellationToken ct)
    {
        var poc = request.PocId is { } id ? await pocs.FindAsync(id, ct) : null;
        if (poc is null || !acts.MayAct(user, poc) || !string.Equals(poc.Target, target.Name, StringComparison.OrdinalIgnoreCase))
            return (null, PocRefusal.NotFound);
        if (!PocStates.IsLive(poc.State)) return (null, PocImportRefusals.NotLive(poc));
        var match = (await identity.CompareAsync(poc.Id, admitted, config.Archive, ct)).Match;
        if (match.Verdict == PocMatchVerdicts.Ask && !request.Confirmed) return (null, PocImportRefusals.DifferentApp(poc, match));
        return await gate.LiveRunAsync(poc, ct) is { } live ? (null, PocImportRefusals.InProgress(poc, live)) : (poc, null);
    }

    public async Task<(Poc? Poc, PocRefusal? Refusal)> NewAsync(
        ClaimsPrincipal user, PocImportRequest request, PocTargetConfig target, PocsConfig config, CancellationToken ct)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (!PocNameProposal.IsName(name)) return (null, PocImportRefusals.BadName(name));
        var owner = SpecDialogOwnership.PrincipalOf(user);
        var holder = await imports.HolderOfAsync(target.Name, name, ct);
        if (holder is not null && !(holder.State == PocStates.Refused && holder.Owner == owner))
            return (null, PocImportRefusals.NameTaken(name, target.Name));
        int? limit = !access.Enforcing || access.HoldsEverywhere(user, Admin) ? null : target.PerPersonLimit;
        if (limit is { } most && await imports.LiveCountAsync(owner, target.Name, ct) is var live && live >= most)
            return (null, PocImportRefusals.OverLimit(target, live));
        if (holder is not null) return await ReviveAsync(holder, ct);
        var provisioned = await provisioner.EnsureAsync(user, config, target, ct);
        if (provisioned.Project is not { } project) return (null, PocRefusal.Conflict(provisioned.Refusal!));
        return await InsertAsync(Row(user, owner, name, project, target), limit, target, ct);
    }

    private async Task<(Poc? Poc, PocRefusal? Refusal)> ReviveAsync(Poc holder, CancellationToken ct)
    {
        if (await gate.LiveRunAsync(holder, ct) is { } live) return (null, PocImportRefusals.InProgress(holder, live));
        await imports.ReviveAsync(holder.Id, ct);
        holder.State = PocStates.Importing;
        return (holder, null);
    }

    private async Task<(Poc? Poc, PocRefusal? Refusal)> InsertAsync(Poc row, int? limit, PocTargetConfig target, CancellationToken ct) =>
        await imports.InsertAsync(row, limit, ct) switch
        {
            PocInsertOutcome.Inserted => (row, null),
            PocInsertOutcome.NameTaken => (null, PocImportRefusals.NameTaken(row.Name, target.Name)),
            _ => (null, PocImportRefusals.OverLimit(target, limit!.Value + 1)),
        };

    private static Poc Row(ClaimsPrincipal user, string owner, string name, string project, PocTargetConfig target) => new()
    {
        Name = name, LiveName = name, Project = project, Target = target.Name, Folder = $"{target.Root}/{project}/{name}",
        Owner = owner, OwnerName = DisplayName.Of(user) ?? owner, State = PocStates.Importing,
    };
}
