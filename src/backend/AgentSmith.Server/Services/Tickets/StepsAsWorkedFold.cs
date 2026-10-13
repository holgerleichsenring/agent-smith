using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59f: folds the ledger transitions the ticket's attempts recorded for a spec into each of
/// its steps — ledger entry ids are the spec's step ids. The state is the step's last recorded one; the
/// attempt and pass are those of the transition that last moved it INTO done, kept only while it stands
/// done. A refused rewrite (done to done) moved nothing.
/// </summary>
public static class StepsAsWorkedFold
{
    private const string Done = "done";

    public static IReadOnlyList<TicketSeriesStepWorkedView> Fold(
        SpecCutEntry spec, IReadOnlyList<RunLedgerTransition> transitions, IReadOnlyDictionary<string, int> attemptNumbers)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(transitions);
        var moves = transitions
            .Where(t => t.SpecId == spec.SpecId && attemptNumbers.ContainsKey(t.RunId))
            .OrderBy(t => attemptNumbers[t.RunId]).ThenBy(t => t.EventSeq ?? long.MaxValue).ThenBy(t => t.Id)
            .SelectMany(t => (RunStoryJson.TryDeserialize<List<LedgerTransitionView>>(t.TransitionsJson) ?? [])
                .Select(v => (Row: t, View: v)))
            .ToList();
        return [.. spec.Steps.Select(step => Step(step.Id, moves.Where(m => m.View.EntryId == step.Id).ToList(), attemptNumbers))];
    }

    private static TicketSeriesStepWorkedView Step(
        string stepId, List<(RunLedgerTransition Row, LedgerTransitionView View)> moves, IReadOnlyDictionary<string, int> numbers)
    {
        var state = moves.Count == 0 ? null : moves[^1].View.To;
        var doneBy = state == Done
            ? moves.LastOrDefault(m => m.View.To == Done && m.View.From != Done).Row
            : null;
        return new TicketSeriesStepWorkedView(
            stepId, state, doneBy?.RunId, doneBy is null ? null : numbers[doneBy.RunId], doneBy?.MasterPass);
    }
}
