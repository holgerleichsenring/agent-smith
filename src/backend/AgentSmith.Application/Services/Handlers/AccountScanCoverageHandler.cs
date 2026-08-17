using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Scans;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0429: hands the scan's account to the ledger the one delivery gate reads.
/// <para>
/// Nothing here decides delivery — <see cref="RunDeliveryGate"/> still does, from the
/// same accounts a coding phase writes. The step only answers what a coding phase's
/// VerifyPhase answers: which ratified criteria the run can show evidence for.
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
        return Task.FromResult(CommandResult.Ok(
            $"Scan coverage: {satisfied}/{account.Criteria.Count} criteria answered"));
    }
}
