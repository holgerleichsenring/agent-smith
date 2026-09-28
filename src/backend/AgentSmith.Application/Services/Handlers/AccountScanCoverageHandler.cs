using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Scans;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0429: hands the scan's account to the ledger the one delivery gate reads, then asks
/// <see cref="RunDeliveryGate"/> whether the scan delivered.
/// <para>
/// A scan has no CommitAndPR, the step that enforces the gate on a coding run, so this step
/// enforces it: an unsatisfied gate fails the run (exit 1) with the gate's reason. The
/// account is recorded first, so result.md still says what was and was not answered.
/// Findings alone never fail a scan — only a scan that could not do what it ratified.
/// </para>
/// </summary>
public sealed class AccountScanCoverageHandler(
    IScanCoverageAccountant accountant,
    ILogger<AccountScanCoverageHandler> logger)
    : ICommandHandler<AccountScanCoverageContext>
{
    public Task<CommandResult> ExecuteAsync(
        AccountScanCoverageContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var account = accountant.Account(context.Pipeline);
        if (account.Criteria.Count == 0)
            return Task.FromResult(CommandResult.Ok("No scan contract to account for"));

        RunAccountLedger.Record(context.Pipeline, [account]);
        foreach (var outstanding in account.Outstanding)
            logger.LogWarning("Scan criterion not answered — {Criterion}: {Note}",
                outstanding.Criterion, outstanding.Note);

        var satisfied = account.Criteria.Count - account.Outstanding.Count;
        logger.LogInformation("Scan coverage: {Satisfied}/{Total} ratified criteria answered",
            satisfied, account.Criteria.Count);

        var verdict = RunDeliveryGate.Evaluate(
            RunAccountLedger.Current(context.Pipeline),
            AcceptanceCriteria.For(context.Pipeline).Count);
        return Task.FromResult(verdict.Satisfied
            ? CommandResult.Ok($"Scan coverage: {satisfied}/{account.Criteria.Count} criteria answered")
            : CommandResult.Fail(verdict.FailureReason ?? "The scan did not deliver what it ratified"));
    }
}
