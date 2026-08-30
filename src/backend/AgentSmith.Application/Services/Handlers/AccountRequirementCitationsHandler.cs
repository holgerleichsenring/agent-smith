using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Scans;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-08-30-03e1: settles what each station of each entry group examined, records the
/// account with the run, and delivers every finding that named an entry of the standard and
/// cited a place this scan read.
/// <para>
/// It decides nothing about delivery. The account is rendered and its gaps are named while
/// <see cref="RunDeliveryGate"/> keeps judging the run on the ratified scan contract alone:
/// a station the scan could not examine for want of an input it was never given would sit
/// outstanding in that ledger forever and fail every scan of every repository it applies to.
/// </para>
/// </summary>
public sealed class AccountRequirementCitationsHandler(
    StationExaminationAccountant accountant,
    ScannerObservationFactory observationFactory,
    ILogger<AccountRequirementCitationsHandler> logger)
    : ICommandHandler<AccountRequirementCitationsContext>
{
    public Task<CommandResult> ExecuteAsync(
        AccountRequirementCitationsContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var account = accountant.Settle(context.Pipeline);
        if (account.IsEmpty)
            return Task.FromResult(CommandResult.Ok("No station was mapped and nothing was cited"));

        context.Pipeline.Set(ContextKeys.ScanExaminationAccount, account);
        observationFactory.AppendObservations(
            context.Pipeline, CitedFindingObservations.For(account));
        Log(account);
        return Task.FromResult(CommandResult.Ok(Summary(account)));
    }

    private void Log(ScanExaminationAccount account)
    {
        foreach (var group in account.NotAttempted)
            logger.LogWarning(
                "Entry group not attempted — {Group}: beyond the {Cap} group(s) one run accounts for",
                group.Group, Tools.CitedFindingLog.MaxEntryGroups);
        foreach (var group in account.Groups.Where(g => g.Attempted))
            logger.LogInformation(
                "Examination — {Group}: {Examined}/{Total} stations examined, {Cited} cited "
                + "finding(s) located",
                group.Group, group.Examined.Count, group.Stations.Count, group.Located.Count);
    }

    private static string Summary(ScanExaminationAccount account) =>
        $"Examination: {account.ExaminedCount}/{account.Stations.Count} stations examined across "
        + $"{account.Groups.Count(g => g.Attempted)} entry group(s), {account.Located.Count} "
        + $"cited finding(s), {account.NotAttempted.Count} not attempted "
        + $"({account.CatalogueVersion})";
}
