using AgentSmith.Application.Services.Pocs.Variables;
using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Services.Acts;
using AgentSmith.Server.Services.Pocs.Import;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Services.Pocs.Variables;

/// <summary>
/// 2026-11-23-933dg: a variable write starts a poc-variables run — the route never writes a variable itself. The rules
/// and, for a secret, the store's master key are checked before anything is written; then the PoC's slot is claimed
/// through the run gate (a live run of the PoC answers 409 naming it) and the run goes through IActRun on the PoC's own
/// project with the claimed run id, in memory, never queued — a secret value is in its seed.
/// </summary>
internal sealed class PocVariableLauncher(
    PocRunGate gate, IActRun runs, PocVariableRules rules, ISecretVault vault, IPocVariableStore store, TicketProjects projects)
{
    /// <summary>Why <paramref name="entries"/> cannot be written to <paramref name="pocId"/> (null: a PoC not written yet).</summary>
    public async Task<PocRefusal?> CheckAsync(long? pocId, IReadOnlyList<PocVariableEntry> entries, CancellationToken ct)
    {
        var existing = pocId is { } id ? await ExistingAsync(id, ct) : [];
        if (rules.Problems(entries, existing, projects.Config().Pocs.Variables) is { Count: > 0 } problems)
            return PocRefusal.BadRequest(string.Join(" ", problems));
        return entries.Any(e => e.Kind == PocVariableKinds.Secret) && vault.Unavailable is { } unavailable
            ? new PocRefusal(StatusCodes.Status503ServiceUnavailable, $"A secret PoC variable cannot be stored now: {unavailable}")
            : null;
    }

    /// <summary>Runs the write in the PoC's project and answers its line; a failed run answers 422 with its line.</summary>
    public async Task<(PocVariablesAnswer? Answer, PocRefusal? Refusal)> RunAsync(
        Poc poc, IReadOnlyList<PocVariableEntry> entries, string person, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(poc);
        if (await CheckAsync(poc.Id, entries, ct) is { } refusal) return (null, refusal);
        var (runId, heldBy) = await gate.ClaimAsync(poc, PipelinePresets.PocVariablesName,
            $"writing {entries.Count} variable(s) of {poc.Name}", ct);
        if (runId is null) return (null, PocImportRefusals.InProgress(poc, heldBy));
        var slot = new ActSlot<string>();
        var result = await runs.RunAsync(poc.Project, PipelinePresets.PocVariablesName, new Dictionary<string, object>
        {
            [ContextKeys.PocVariableWrite] = new PocVariableWrite(poc.Id, entries, person), [ContextKeys.PocVariableReport] = slot,
        }, runId: runId);
        var line = slot.IsSet ? slot.Value! : result.Message;
        return result.IsSuccess
            ? (new PocVariablesAnswer(runId, line), null)
            : (null, new PocRefusal(StatusCodes.Status422UnprocessableEntity, $"{line} (run {runId})"));
    }

    private async Task<IReadOnlyList<string>> ExistingAsync(long pocId, CancellationToken ct)
    {
        var secrets = (await vault.StatesAsync(SecretScope.Poc(pocId), ct)).Select(s => s.Name);
        var plain = (await store.ReadAsync(pocId, ct))?.Plain.Select(p => p.Name) ?? [];
        return [.. secrets.Concat(plain)];
    }
}
