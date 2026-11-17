using AgentSmith.Application.Services.Filing;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.TicketActs;

/// <summary>
/// 2026-09-25-8e51e (2026-11-17-e061c: moved from the server's TicketAmendment into the 'amend' run): THE RECORD AND THE
/// BRANCH ARE WHAT A RUN READS, so they are written on every tracker. The description rewrite is a courtesy, never
/// blocking, and it goes FIRST, so the branch set's fingerprint is the text the tracker stored.
/// </summary>
public sealed class TicketAmendmentWrite(
    ApprovedSpecSetRecorder approvals, ITicketProviderFactory trackers, FiledSpecBranch branches,
    TicketDescriptionRewrite descriptions, ILogger<TicketAmendmentWrite> logger)
{
    /// <summary>What the thread is told. Throws when the record could not be stored.</summary>
    public async Task<string> WriteAsync(ResolvedProject project, AmendTicketRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var id = new TicketId(request.TicketId);
        var rewrite = await descriptions.WriteAsync(project, id, request.Region, request.Previous, ct);
        var record = await approvals.RecordAsync(request.Approval, project, request.TicketId, request.Set, request.Goal, ct, request.Continuation);
        // Read back AFTER the rewrite: the fingerprint the branch set carries is of the ticket as the TRACKER stored it,
        // and a stale one reads as a ticket edit on the next run. Each spec goes into the repository it names.
        var branch = await branches.WriteAsync(project, record, id, await ReadBackAsync(project, id, ct), ct, request.Set.Sources);
        logger.LogInformation("Approval recorded under ticket {Ticket} from conversation {Session}: {Specs} spec(s), branch {State}",
            request.TicketId, request.Approval.Conversation, request.Set.Drafts.Count, branch.Written ? "written" : branch.Error);
        var recorded = $"The approved series under ticket {request.TicketId} is the one you just approved "
            + $"({request.Set.Drafts.Count} spec(s)). {rewrite}{string.Concat((branch.Notes ?? []).Select(n => " " + n))}";
        return branch.Written ? recorded
            : $"{recorded} The series is NOT on the ticket branch yet ({branch.Error}); a run that claims "
                + "this ticket works the set the branch still carries — approve again once it can be written.";
    }

    private async Task<Ticket?> ReadBackAsync(ResolvedProject project, TicketId id, CancellationToken ct)
    {
        try { return await trackers.Create(project.Tracker).GetTicketAsync(id, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The ticket {Ticket} could not be read back", id.Value);
            return null;
        }
    }
}
