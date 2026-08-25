using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0420: takes each repository's account of the phase — the ratified criteria against
/// what the branch actually delivers.
/// <para>
/// Its own type rather than more handler: the handler decides WHICH commands verify a
/// repository, this decides whether the phase was carried out, and those are different
/// questions asked of different evidence.
/// </para>
/// </summary>
public sealed class PhaseAccounting(
    DeliveryDiff deliveryDiff,
    ISpecAccountant accountant,
    SandboxTargets sandboxTargets,
    ILogger<PhaseAccounting> logger)
{
    public async Task<IReadOnlyList<SpecAccount>> TakeAsync(
        PipelineContext pipeline,
        IReadOnlyDictionary<string, ISandbox> sandboxes,
        IReadOnlyList<string> commandResults,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(sandboxes);

        var criteria = AcceptanceCriteria.For(pipeline);
        if (criteria.Count == 0)
        {
            logger.LogInformation(
                "No ratified criteria for this phase — nothing to account for");
            return [];
        }

        var agent = pipeline.Resolved().Agent;
        var costTracker = PipelineCostTracker.GetOrCreate(pipeline);

        // ONE account for the phase, over every repository's diff at once. Per-repo
        // accounting asked each repository to satisfy criteria written about ANOTHER — a
        // two-repo ticket whose criteria name the API service made the worker repository
        // outstanding on every one of them, which is a false negative by construction.
        // The criteria belong to the PHASE; the repositories are where its work lands.
        var evidence = await DeliveryEvidence.GatherAsync(
            deliveryDiff, sandboxes, cancellationToken);
        if (evidence.Failures.Count > 0)
            return [new SpecAccount(
                string.Join(", ", sandboxes.Keys), [],
                $"the delivery diff could not be taken for {string.Join("; ", evidence.Failures)}")];

        SpecAccount account;
        try
        {
            // p0483: the sandboxes are still standing here, so the account can look at the
            // branch instead of being told about it.
            account = await accountant.AccountAsync(
                string.Join(", ", sandboxes.Keys), criteria, evidence.Diff,
                commandResults, agent, new BranchSearch(sandboxes, logger, evidence.BaseRefs),
                costTracker, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            account = new SpecAccount(
                string.Join(", ", sandboxes.Keys), [],
                $"the account could not be taken — {ex.Message}");
        }
        foreach (var (level, message) in SpecAccountLog.Lines(account))
            logger.Log(level, "{AccountLine}", message);
        return [account];
    }

    /// <summary>
    /// p0421, found in run 8a1f: a run whose phases were ALL already executed on the branch
    /// runs no phase, so nothing accounts for anything — and the gate, correctly refusing an
    /// unaccounted run, then failed exactly the case the accounting exists for. Whether the
    /// branch satisfies the criteria does not depend on a phase running now.
    /// </summary>
    public async Task<RunAccounts> TakeOrReuseAsync(
        PipelineContext pipeline, IReadOnlyList<string> criteria, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(criteria);
        var existing = RunAccountLedger.Current(pipeline);
        if (existing.All.Count > 0 || criteria.Count == 0) return existing;
        if (!sandboxTargets.TryResolve(pipeline, out var sandboxes, out _)) return existing;

        logger.LogInformation(
            "No phase ran in this run — accounting for the branch against its {Count} criteria here",
            criteria.Count);
        RunAccountLedger.Record(pipeline, await TakeAsync(pipeline, sandboxes, [], ct));
        return RunAccountLedger.Current(pipeline);
    }
}
