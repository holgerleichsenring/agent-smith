using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-22-b6ad: puts an APPROVED set on the ticket branch at FILING time, so the
/// specification exists before any run does — something to open, review and edit between the
/// approval and the first claim, where today there is only a database row.
/// <para>
/// No sandbox, no clone and no pipeline: the source provider writes the files on the branch
/// directly, and the branch is created at the default branch's head when it is not there.
/// </para>
/// <para>
/// REVISION 1 IS MINTED HERE. The stored record deliberately carries no revisions — numbering
/// was the run's bookkeeping — but a set's current revision is the last element of that list, so
/// an empty one throws on read, and an index written with none reads back as revision 1 "initial
/// derivation", which is the one thing this set is not. The cause names the approval and the
/// conversation it was given in, spelled by the same <see cref="ApprovedSetHandoff"/> the run's own
/// approved arm composes it with.
/// </para>
/// <para>
/// THE POINTER IS RECORDED WITH IT. A spec-path commit whose sha is not the one this system
/// recorded is read as a REVIEWER'S EDIT, and an absent pointer reads the same way.
/// </para>
/// <para>
/// 2026-10-12-f5f7d: each spec goes to the repository it names (<see cref="SeriesRepos"/>), every
/// member its own sub-series with one stamp; a spec naming none belongs to the pointer's carrying
/// repository, else the approval's. What already ran is read BY ID — a series over several
/// repositories has no leading count — so an amendment never writes an executed spec back.
/// </para>
/// </summary>
public sealed class FiledSpecBranch(
    FiledSeriesWrite writes,
    ISpecSetPointerStore stored,
    SeriesRepos placer,
    SpecSetPointerRecorder pointers,
    ILogger<FiledSpecBranch> logger)
{
    /// <param name="ticket">The ticket as the TRACKER stored it, which is what the set is
    /// fingerprinted from — never the body we rendered.</param>
    public async Task<BranchWriteResult> WriteAsync(
        ResolvedProject project, SpecApprovalRecord record, TicketId ticketId, Ticket? ticket,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(record);
        // A set whose fingerprint is null can never acquire one: the refresh fires when the model
        // ran, when there was no previous set, or when an edit was seen — and this write makes
        // "no previous set" false from the first run onward, while an approved set never runs the
        // model and seeing an edit needs a fingerprint to compare against. Writing the branch
        // without one would silence the kept-edit notice for this ticket permanently, without a
        // failure anywhere, so the branch is not written at all.
        if (ticket is null)
            return BranchWriteResult.Failed(
                "the ticket could not be read back, so the set would carry no ticket fingerprint");
        var pointer = await stored.GetAsync(project.Name, record.Set.Key, cancellationToken);
        var placement = placer.Place(Published(record, ticket, pointer?.ExecutedIdsOf(record.Set.Specs) ?? []),
            new SeriesScope(project.Repos, SeriesScope.LegacyOf(pointer, record.CarryingRepo, project.Repos), [],
                pointer?.CarryingRepo));
        if (placement.Refusal is { } refused) return BranchWriteResult.Failed(refused);
        try
        {
            return await WriteAsync(project, placement, ticketId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Writing the approved set {Key} to the ticket branch failed", record.Set.Key);
            return BranchWriteResult.Failed(ex.Message.Split('\n', 2)[0].Trim());
        }
    }

    private async Task<BranchWriteResult> WriteAsync(
        ResolvedProject project, SeriesPlacement placement, TicketId ticketId, CancellationToken ct)
    {
        var set = placement.Set;
        var written = await writes.WriteAsync(placement, TicketBranchNamer.Compose(ticketId), ct);
        if (!written.Written)
        {
            logger.LogWarning("The approved set {Key} was not written to the ticket branch of {Repos}: {Error}",
                set.Key, string.Join(", ", placement.Names), written.Error);
            return BranchWriteResult.Failed(written.Error ?? "the write named no commit");
        }
        await pointers.RecordAsync(project.Name, placement.Carrier?.Name, set, written.Shas, ct);
        logger.LogInformation(
            "The approved set {Key} ({Specs} spec(s)) was written to the ticket branch of {Repos} as {Shas}",
            set.Key, set.Specs.Count, string.Join(", ", placement.Names), string.Join(", ", written.Shas.Values));
        return BranchWriteResult.Ok(written.CommitSha!);
    }

    // 2026-10-06-03c7f: the manifest's goal and approval are the stored row's, carried on its set
    // unchanged — the approval instant exactly as the record holds it.
    // 2026-10-12-f5f7d: the specs the record step moved to done/ are executed by id, so an amendment
    // renders only the others into planned/.
    private static SpecSet Published(SpecApprovalRecord record, Ticket ticket, IReadOnlyList<string> executed) =>
        record.Set with
        {
            Revisions = [new SpecRevision(1, ApprovedSetHandoff.CauseOf(record), DateTimeOffset.UtcNow)],
            TicketFingerprint = TicketTextFingerprint.Of(ticket),
            Executed = [.. executed],
        };
}
