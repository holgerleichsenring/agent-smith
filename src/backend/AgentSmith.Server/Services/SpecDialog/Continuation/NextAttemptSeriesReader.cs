using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using YamlDotNet.Core;

namespace AgentSmith.Server.Services.SpecDialog.Continuation;

/// <summary>
/// The series a ticket's next attempt will read: the continuation basis (the latest published cut with specs, else
/// the approval record's set, across the projects sharing the tracker) with the pending edit
/// (<see cref="PendingSeriesEditReader"/>) merged over it by the same <see cref="ContinuationMerge"/> the attempt
/// applies. Reading, editing and verifying the series all start from this one reading.
/// </summary>
public sealed class NextAttemptSeriesReader(
    TicketContinuationStateReader states, PendingSeriesEditReader pending, ContinuationMerge merge, SpecDraftReader drafts)
{
    public async Task<NextAttemptSeries> ReadAsync(ResolvedProject project, string ticketId, bool mayStartRuns, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        var read = await states.ReadAsync(project.Name, ticketId, mayStartRuns, ct);
        if (read.State is not { } state) return new(read, []);
        var basis = state.Basis.DistinctBy(b => b.SpecId).Select(Draft).ToList();
        if (await pending.ReadAsync(project, ticketId, ct) is not { Continuation: { } continuation } edit) return new(read, basis);
        var branch = new SpecSet(edit.Key, [.. basis.Select(d => new DerivedSpec(d, string.Empty, string.Empty, []))],
            SpecAccounting.Empty, [], SpecSource.BranchArtifact, ExecutedSpecIds: state.ExecutedIds, Series: state.Series);
        var (merged, _) = merge.Apply(branch, edit.Set, continuation);
        return new(read, [.. merged.Select(s => s.Draft)], edit);
    }

    // The cut's yaml verbatim: an edit compares a re-cut with it byte for byte. A cut entry that is not a full spec
    // document still names its id and goal, and declares no edge.
    private SpecDraft Draft(BasisSpec spec)
    {
        try
        {
            return drafts.Read(spec.Yaml) with { SpecId = spec.SpecId, Goal = spec.Goal, Yaml = spec.Yaml };
        }
        catch (Exception ex) when (ex is InvalidOperationException or YamlException)
        {
            return new SpecDraft(spec.SpecId, spec.Goal, spec.Yaml, []);
        }
    }
}
