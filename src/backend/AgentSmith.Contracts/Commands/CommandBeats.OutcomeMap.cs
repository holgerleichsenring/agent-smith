namespace AgentSmith.Contracts.Commands;

/// <summary>
/// 2026-08-30: the outcome beat's own table. Split out when two phases each added one
/// step and the combined map crossed the file-length rule — the rule says split the
/// responsibilities rather than raise a baseline, and the outcome beat (ship the result)
/// is the one group that reads as a subject of its own.
/// <para>
/// A METHOD, not a static field: C# gives no guarantee about the order in which static
/// field initialisers across partial files run, so a field here would sometimes be read
/// before it exists. A method is resolved when it is called.
/// </para>
/// </summary>
public static partial class CommandBeats
{
    private static Dictionary<string, RunBeat> OutcomeBeats() => new(StringComparer.Ordinal)
    {
        [CommandNames.WriteRunResult] = RunBeat.Outcome,
        [CommandNames.WritePhaseRecord] = RunBeat.Outcome,
        [CommandNames.CommitAndPR] = RunBeat.Outcome,
        [CommandNames.InitCommit] = RunBeat.Outcome,
        [CommandNames.PrCrossLink] = RunBeat.Outcome,
        [CommandNames.InitComplete] = RunBeat.Outcome,
        [CommandNames.PersistWorkBranch] = RunBeat.Outcome,
        [CommandNames.CollectSpecDialogReply] = RunBeat.Outcome,
        [CommandNames.DeliverOutput] = RunBeat.Outcome,
        [CommandNames.DeliverFindings] = RunBeat.Outcome,
        [CommandNames.PostPrComments] = RunBeat.Outcome,
        [CommandNames.SecuritySnapshotWrite] = RunBeat.Outcome,
        [CommandNames.SpawnFix] = RunBeat.Outcome,
        [CommandNames.WriteTickets] = RunBeat.Outcome,
    };
}
