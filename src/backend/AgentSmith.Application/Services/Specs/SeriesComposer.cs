using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-11-17-a0b6a: turns the sub-series read from a ticket's repositories into its ONE set — the
/// join and the set's composition from the newest manifest, what <see cref="SeriesReader"/> did after
/// reading. Shared by the run's checkout read and the host-side branch read, so both answer the same
/// set for the same files.
/// </summary>
public sealed class SeriesComposer(SeriesJoin join, SeriesManifest manifest, ILogger<SeriesComposer> logger)
{
    public SpecSetOnBranch Compose(TicketKey ticket, string carrying, IReadOnlyList<SubSeriesRead> reads)
    {
        var joined = join.Join(reads, carrying);
        if (joined.Otherwise is { } answer)
        {
            if (answer.State == SpecSetBranchState.Unreadable)
                logger.LogWarning("The series of ticket {Ticket} is unreadable: {Why}", ticket.Value, answer.Why);
            return answer;
        }
        return SpecSetOnBranch.Answered(ResultOf(ticket, carrying, joined));
    }

    private SpecSetReadResult ResultOf(TicketKey ticket, string carrying, SeriesJoined joined)
    {
        var newest = joined.Newest!;
        var set = SetOf(ticket, newest.Base, newest.Document!, joined.Specs, joined.Executed);
        var sha = joined.Shas.TryGetValue(carrying, out var own) ? own : joined.Shas[newest.Repo];
        logger.LogInformation(
            "Series {Base} of {Ticket} read from {Repos}: {Specs} spec(s), revision {Revision}, lagging {Lagging}",
            newest.Base, set.Key, string.Join(", ", joined.Shas.Keys), set.Specs.Count, set.Current.Number,
            joined.Lagging.Count == 0 ? "none" : string.Join(", ", joined.Lagging));
        return new SpecSetReadResult(set, sha)
        {
            RevisionShas = joined.Shas, Lagging = joined.Lagging,
            // 2026-10-12-f5f7f: what a re-cut demand on an out-of-step series cuts from.
            OutOfStepPrevious = joined.OutOfStep is var (specs, executed)
                ? SetOf(ticket, newest.Base, newest.Document!, specs, executed) : null,
        };
    }

    private SpecSet SetOf(
        TicketKey ticket, string seriesBase, SeriesManifestDocument doc, IReadOnlyList<DerivedSpec> specs,
        IReadOnlyList<string> executed) =>
        new(ticket.Value, specs, manifest.AccountingOf(doc), manifest.RevisionsOf(doc),
            SpecSource.BranchArtifact, manifest.HandbackOf(doc), doc.TicketPinnedWhole,
            executed, manifest.FingerprintOf(doc), manifest.ApprovalOf(doc), seriesBase)
        {
            Goal = string.IsNullOrWhiteSpace(doc.Goal) ? null : doc.Goal,
            Contract = SeriesContractYaml.Read(doc.Contract), // 2026-10-13-8762j
        };
}
