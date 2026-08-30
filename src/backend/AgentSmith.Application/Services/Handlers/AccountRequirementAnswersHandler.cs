using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Scans;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-08-30-3c12: settles the answers the scan gave to the standard's entries, records
/// the account with the run, and names every entry it says is unmet, could not decide, or
/// never reached.
/// <para>
/// It decides nothing about delivery. The account is rendered and its gaps are named while
/// <see cref="RunDeliveryGate"/> keeps judging the run on the ratified scan contract alone:
/// an entry undecidable for want of an input the scan was never given would sit outstanding
/// in that ledger forever and fail every scan of every repository it applies to.
/// </para>
/// </summary>
public sealed class AccountRequirementAnswersHandler(
    RequirementAccountant accountant,
    ScannerObservationFactory observationFactory,
    ILogger<AccountRequirementAnswersHandler> logger)
    : ICommandHandler<AccountRequirementAnswersContext>
{
    public Task<CommandResult> ExecuteAsync(
        AccountRequirementAnswersContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var account = accountant.Settle(context.Pipeline);
        if (account.IsEmpty)
            return Task.FromResult(CommandResult.Ok("No requirement was answered"));

        context.Pipeline.Set(ContextKeys.RequirementAccount, account);
        observationFactory.AppendObservations(context.Pipeline, RequirementFindings.For(account));
        Log(account);
        return Task.FromResult(CommandResult.Ok(Summary(account)));
    }

    private void Log(RequirementAccount account)
    {
        foreach (var group in account.NotAttempted)
            logger.LogWarning(
                "Entry group not attempted — {Group}: beyond the {Cap} group(s) one run answers for",
                group.Group, Tools.RequirementAnswerLog.MaxEntryGroups);
        foreach (var group in account.Groups.Where(g => g.Attempted))
            logger.LogInformation(
                "Requirements — {Group}: {Answered}/{Total} answered, {Unmet} unmet, "
                + "{Undecidable} undecidable, writes enumerated: {Writes}",
                group.Group, group.Answered.Count, group.Rows.Count, group.Unmet.Count,
                group.Undecidable.Count, group.EnumeratesWrites);
    }

    private static string Summary(RequirementAccount account) =>
        $"Requirements: {account.AnsweredCount}/{account.Rows.Count} answered across "
        + $"{account.Groups.Count(g => g.Attempted)} entry group(s), "
        + $"{account.NotAttempted.Count} not attempted ({account.CatalogueVersion})";
}
