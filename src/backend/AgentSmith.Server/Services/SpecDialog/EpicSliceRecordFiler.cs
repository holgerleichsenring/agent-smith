using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-17-0e79d: the RECORDS of an approved epic — one ticket per slice, carrying the record
/// label, the requirement body and no stamp at all, linked to the work ticket through the
/// tracker's own relation. They are read by a person: nothing routes them and no machine reads
/// them, because the run works the set stored under the work ticket.
/// <para>
/// NOTHING HERE IS THE FILING'S ERROR. By the time this runs the work ticket exists and carries
/// the whole approved set, so the epic is complete and runnable. An error would tell the operator
/// to retry, and the retry files a SECOND work ticket with a SECOND stored set — two runs and two
/// pull requests per repository. A record that could not be created, and a link that did not
/// land, are therefore NOTES on the filing report, naming the slice that has no ticket.
/// </para>
/// </summary>
public sealed class EpicSliceRecordFiler(
    PhaseTicketRenderer renderer,
    ILogger<EpicSliceRecordFiler> logger)
{
    /// <summary>One reference line per record that was created, in the set's order.</summary>
    public async Task<IReadOnlyList<string>> FileAsync(
        ITicketProvider provider, IReadOnlyList<PhaseDraft> slices, CreatedTicket work,
        List<FiledTicket> filed, List<string> notes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(slices);
        var refs = new List<string>(slices.Count);
        var siblingIds = slices.Select(s => s.PhaseId).ToHashSet(StringComparer.Ordinal);
        foreach (var slice in slices)
        {
            var content = renderer.RenderChildRequirement(slice, siblingIds);
            if (await CreateAsync(provider, content, slice, notes, ct) is not { } record) continue;
            filed.Add(new FiledTicket(record.Reference, content.Title));
            if (await LinkAsync(provider, record, work, ct) is { Outcome: not ParentLinkOutcome.Linked } link)
                notes.Add($"{record.Reference} is not linked to its work ticket {work.Reference}: {link.Reason}");
            refs.Add($"{record.Reference} — `{slice.PhaseId}` {slice.Goal}");
        }
        return refs;
    }

    private async Task<CreatedTicket?> CreateAsync(
        ITicketProvider provider, PhaseTicketContent content, PhaseDraft slice,
        List<string> notes, CancellationToken ct)
    {
        try
        {
            // A record must not route, and "no label" is not the way to say that: a ticket with no
            // framework label is routed by the project's own rules. The record label is refused.
            return await provider.CreateAsync(
                content.Title, content.Body, [PhaseTicketRenderer.EpicLabel], ct);
        }
        // A timeout is a record that was not filed; only the caller's own cancellation stops the run.
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Filing the slice record for {Slice} threw", slice.PhaseId);
            notes.Add(
                $"no record ticket was filed for `{slice.PhaseId}` {slice.Goal}: {ex.Message}. "
                + "The work ticket carries the whole approved set, so the run works this slice "
                + "either way — file the record by hand if a reader needs it.");
            return null;
        }
    }

    private async Task<ParentLinkResult> LinkAsync(
        ITicketProvider provider, CreatedTicket record, CreatedTicket work, CancellationToken ct)
    {
        try
        {
            return await provider.LinkToParentAsync(record, work.Id, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Linking {Record} to its work ticket {Work} threw", record.Reference, work.Reference);
            return ParentLinkResult.Failed(ex.Message);
        }
    }
}
