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
/// </summary>
public sealed class FiledSpecBranch(
    ISourceProviderFactory sources,
    SpecSetFiles files,
    SpecSetPointerRecorder pointers,
    ILogger<FiledSpecBranch> logger)
{
    /// <param name="carrier">
    /// The repository to write into. Null means nobody handed one over, and the set goes to the
    /// first of the project's configured repositories that the approval named — the same rule the
    /// run's own resolver applies, and the one the record already recorded.
    /// </param>
    /// <param name="ticket">The ticket as the TRACKER stored it, which is what the set is
    /// fingerprinted from — never the body we rendered.</param>
    public async Task<BranchWriteResult> WriteAsync(
        ResolvedProject project, SpecApprovalRecord record, TicketId ticketId, Ticket? ticket,
        RepoConnection? carrier, CancellationToken cancellationToken)
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
        var chosen = Chosen(project, record, carrier);
        if (chosen is null)
            return BranchWriteResult.Failed(
                $"the approval named no configured repository of project '{project.Name}' to carry the set");

        var set = Published(record, ticket);
        var key = new SpecSetKey(set.Key);
        try
        {
            var result = await sources.Create(chosen).WriteFilesToBranchAsync(
                TicketBranchNamer.Compose(ticketId), Rendered(key, set), MessageFor(set),
                cancellationToken);
            if (!result.Written)
            {
                logger.LogWarning(
                    "The approved set {Key} was not written to the ticket branch of {Repo}: {Error}",
                    set.Key, chosen.Name, result.Error);
                return result.Error is null ? BranchWriteResult.Failed("the write named no commit") : result;
            }
            await pointers.RecordAsync(project.Name, chosen, set, result.CommitSha!, cancellationToken);
            logger.LogInformation(
                "The approved set {Key} ({Phases} phase(s)) was written to the ticket branch of {Repo} as {Sha}",
                set.Key, set.Phases.Count, chosen.Name, result.CommitSha);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Writing the approved set {Key} to the ticket branch failed", set.Key);
            return BranchWriteResult.Failed(ex.Message.Split('\n', 2)[0].Trim());
        }
    }

    private IReadOnlyList<RepoFile> Rendered(SpecSetKey key, SpecSet set) =>
        [.. files.Render(key, set, []).Select(f => new RepoFile(f.Path, f.Content))];

    // The approval instant travels in the index exactly as the record carries it, so the
    // precedence compares equal instants and the branch — which wins a tie — stands.
    private static SpecSet Published(SpecApprovalRecord record, Ticket ticket) =>
        record.Set with
        {
            Revisions = [new SpecRevision(1, ApprovedSetHandoff.CauseOf(record), DateTimeOffset.UtcNow)],
            TicketFingerprint = TicketTextFingerprint.Of(ticket),
        };

    private static RepoConnection? Chosen(
        ResolvedProject project, SpecApprovalRecord record, RepoConnection? carrier) =>
        carrier
        ?? Named(project, record.CarryingRepo)
        ?? Named(project, SpecCarryingRepoResolver.ChooseCarrier(project.Repos, record.Repositories));

    private static RepoConnection? Named(ResolvedProject project, string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : project.Repos.FirstOrDefault(
            r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));

    private static string MessageFor(SpecSet set) =>
        $"spec: {set.Key} revision {set.Current.Number} ({set.Current.Cause}), "
        + $"{set.Phases.Count} phase(s)";
}
