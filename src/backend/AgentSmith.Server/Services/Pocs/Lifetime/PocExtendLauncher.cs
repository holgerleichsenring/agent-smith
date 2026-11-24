using System.Globalization;
using System.Security.Claims;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Acts;
using AgentSmith.Server.Services.Pocs.Import;
using AgentSmith.Server.Services.SpecDialog;

namespace AgentSmith.Server.Services.Pocs.Lifetime;

/// <summary>
/// 2026-11-23-c81ee: an extension starts a poc-extend run — the route never changes the PoC itself. The PoC's slot is
/// claimed through the run gate (a live run answers 409 naming it), then the run goes through IActRun on the PoC's project
/// with the claimed run id, awaited, so the button gets its answer. IActRun runs on no token, so whether the caller holds
/// pocs.admin travels as the seed ContextKeys.PocMayExceed. A refused run answers 422 with its sentence.
/// </summary>
internal sealed class PocExtendLauncher(PocRunGate gate, IActRun runs, PocActAccess access, PocRepository pocs)
{
    public async Task<(PocExtendAnswer? Answer, PocRefusal? Refusal)> ExtendAsync(ClaimsPrincipal user, Poc poc, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(poc);
        if (!PocStates.IsLive(poc.State)) return (null, PocRefusal.Conflict($"'{poc.Name}' is {poc.State}; there is nothing to extend."));
        var (runId, heldBy) = await gate.ClaimAsync(poc, PipelinePresets.PocExtendName, $"extending {poc.Name}", ct);
        if (runId is null) return (null, PocImportRefusals.InProgress(poc, heldBy));
        var result = await runs.RunAsync(poc.Project, PipelinePresets.PocExtendName, new Dictionary<string, object>
        {
            [ContextKeys.PocId] = poc.Id.ToString(CultureInfo.InvariantCulture), [ContextKeys.PocActor] = SpecDialogOwnership.PrincipalOf(user),
            [ContextKeys.PocMayExceed] = access.IsAdmin(user) ? "true" : "false",
        }, runId: runId);
        if (!result.IsSuccess) return (null, new PocRefusal(StatusCodes.Status422UnprocessableEntity, $"{result.Message} (run {runId})"));
        var extended = await pocs.FindAsync(poc.Id, ct) ?? poc;
        var expires = extended.ExpiresAtTicks is { } ticks ? new DateTimeOffset(ticks, TimeSpan.Zero) : DateTimeOffset.MinValue;
        return (new PocExtendAnswer(poc.Id, runId, expires, extended.Extensions), null);
    }
}
