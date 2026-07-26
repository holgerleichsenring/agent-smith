using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Memory;

/// <summary>
/// p0380: MANDATORY on the green path — a keystone-green ticket run writes ONE
/// curated `project` memory (entry + index line) via the MemoryStore. Failed/
/// noisy runs write nothing. A failed memory write never fails the run, but
/// the miss is logged LOUDLY — a skipped green-run narrative is a defect, not
/// an option.
/// </summary>
public sealed class RunNarrativeMemoryWriter(ILogger<RunNarrativeMemoryWriter> logger)
{
    /// <summary>Returns true when the narrative entry was written.</summary>
    public async Task<bool> TryWriteAsync(
        ISandboxFileReader reader, string repoRoot, Ticket? ticket, string runId,
        string? failureReason, IReadOnlyList<CodeChange> changes,
        IReadOnlyList<PlanDecision>? decisions, CancellationToken ct)
    {
        if (failureReason is not null || ticket is null) return false;
        var entry = RunNarrativeComposer.Compose(ticket, runId, changes, decisions);
        try
        {
            var store = new MemoryStore(reader, repoRoot, logger);
            await store.UpsertAsync(entry, ct);
            logger.LogInformation(
                "Green run {RunId}: curated project memory '{Name}' written to memory/MEMORY.md",
                runId, entry.Name);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "DEFECT: green run {RunId} could NOT write its mandatory curated memory '{Name}' — "
                + "the run narrative in .agentsmith/memory/ is now missing this run",
                runId, entry.Name);
            return false;
        }
    }
}
