using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Scans;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-08-30-18e3: checks the entry map the scan master stated, records it with the run,
/// and names every station nothing located.
/// <para>
/// The instruction already existed — the master's first phase has always been told to
/// enumerate the entry points, the trust boundaries and where credentials are handled. What
/// was missing is a step that COLLECTS that enumeration and checks it. This is that step,
/// and it decides nothing about delivery: the map is rendered and its gaps are named, while
/// <see cref="RunDeliveryGate"/> keeps judging the run on the ratified scan contract alone.
/// </para>
/// </summary>
public sealed class AccountEntryStationsHandler(
    StationMapResolver resolver,
    ScannerObservationFactory observationFactory,
    ILogger<AccountEntryStationsHandler> logger)
    : ICommandHandler<AccountEntryStationsContext>
{
    public Task<CommandResult> ExecuteAsync(
        AccountEntryStationsContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var map = resolver.Resolve(context.Pipeline);
        if (map.IsEmpty)
            return Task.FromResult(CommandResult.Ok("No entry map was stated"));

        context.Pipeline.Set(ContextKeys.RequestStationMap, map);
        var unlocated = map.Unlocated;
        observationFactory.AppendObservations(context.Pipeline, UnlocatedStationFindings.For(map));

        foreach (var (group, station) in unlocated)
            logger.LogWarning("Entry station not located — {Group} / {Station}: {Note}",
                group, station.Station, station.Note);

        var total = map.Groups.Count * Enum.GetValues<VerificationStation>().Length;
        logger.LogInformation(
            "Entry map: {Located}/{Total} stations located across {Groups} entry group(s)",
            total - unlocated.Count, total, map.Groups.Count);
        return Task.FromResult(CommandResult.Ok(
            $"Entry map: {total - unlocated.Count}/{total} stations located across "
            + $"{map.Groups.Count} entry group(s)"));
    }
}
