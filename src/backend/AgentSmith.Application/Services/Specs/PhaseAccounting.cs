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
    SpecAccountant accountant,
    ILogger<PhaseAccounting> logger)
{
    public async Task<IReadOnlyList<SpecAccount>> TakeAsync(
        PipelineContext pipeline,
        IReadOnlyDictionary<string, ISandbox> sandboxes,
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
        var accounts = new List<SpecAccount>();
        foreach (var (key, sandbox) in sandboxes)
        {
            var diff = await deliveryDiff.ForBranchAsync(sandbox, cancellationToken);
            accounts.Add(await AccountAsync(key, criteria, diff, agent, costTracker, cancellationToken));
        }
        return accounts;
    }

    private async Task<SpecAccount> AccountAsync(
        string key, IReadOnlyList<string> criteria,
        DeliveryDiff.DiffResult diff, AgentConfig agent,
        PipelineCostTracker costTracker, CancellationToken ct)
    {
        // A diff that could not be taken is not an empty diff: accounting against
        // "nothing changed" would fail every criterion for an infrastructure reason.
        if (diff.Failed)
            return new SpecAccount(key, [], $"the delivery diff could not be taken ({diff.Basis})");

        var account = await accountant.AccountAsync(key, criteria, diff.Text, agent, costTracker, ct);
        LogAccount(key, diff, account);
        return account;
    }

    private void LogAccount(string key, DeliveryDiff.DiffResult diff, SpecAccount account)
    {
        if (account.Problem is not null)
        {
            logger.LogWarning("{Repo}: no account could be taken — {Problem}", key, account.Problem);
            return;
        }
        if (account.Delivered)
        {
            logger.LogInformation(
                "{Repo}: all {Count} criteria accounted for {Basis}", key, account.Criteria.Count, diff.Basis);
            return;
        }
        foreach (var outstanding in account.Outstanding)
            logger.LogWarning("{Repo}: OUTSTANDING — {Criterion}{Note}",
                key, outstanding.Criterion,
                outstanding.Note is null ? string.Empty : $" ({outstanding.Note})");
    }
}
