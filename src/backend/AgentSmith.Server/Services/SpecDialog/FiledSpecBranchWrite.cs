using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-22-b6ad: writing a filed ticket's approved set to its branch, and the NOTE it leaves
/// when that did not happen. It sits beside the filer rather than inside it because it answers a
/// question of its own — what a tracker call that fails AFTER the ticket exists costs.
/// <para>
/// The set is fingerprinted from the ticket AS THE TRACKER STORED IT, read back through the
/// provider that created it: the comparison a later run makes is taken over a fetched ticket's
/// text, and a tracker that round-trips markup would make a fingerprint taken over the body we
/// rendered differ from the very next read and report a spurious edit on the first run.
/// </para>
/// </summary>
public sealed class FiledSpecBranchWrite(
    FiledSpecBranch branches,
    ILogger<FiledSpecBranchWrite> logger)
{
    /// <param name="notes">
    /// What went wrong without unfiling anything. A failed write belongs here and not in the
    /// filing's error: the ticket and the record are complete, so an error would tell the operator
    /// to retry and the retry files a SECOND ticket with a second stored set.
    /// </param>
    public async Task WriteAsync(
        ITicketProvider provider, ResolvedProject project, CreatedTicket created,
        SpecApprovalRecord record, List<string> notes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(created);
        ArgumentNullException.ThrowIfNull(notes);
        var result = await branches.WriteAsync(
            project, record, created.Id, await ReadBackAsync(provider, created, ct), carrier: null, ct);
        if (result.Written) return;
        logger.LogWarning(
            "The approved set of ticket {Ticket} is not on its branch: {Error}",
            created.Reference, result.Error);
        notes.Add(
            $"The specification of {created.Reference} is not on its ticket branch yet "
            + $"({result.Error}). The ticket and its approved set are complete — the run that "
            + "claims it publishes the set to the branch from the stored record, as it did before.");
    }

    private async Task<Ticket?> ReadBackAsync(
        ITicketProvider provider, CreatedTicket created, CancellationToken ct)
    {
        try { return await provider.GetTicketAsync(created.Id, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(
                ex, "The ticket {Ticket} could not be read back after it was created", created.Reference);
            return null;
        }
    }
}
