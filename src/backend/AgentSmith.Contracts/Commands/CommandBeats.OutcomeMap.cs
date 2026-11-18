namespace AgentSmith.Contracts.Commands;

/// <summary>
/// 2026-08-30: the outcome beat's own table. Split out when two specs each added one
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
        [CommandNames.WriteSpecRecord] = RunBeat.Outcome,
        [CommandNames.CommitAndPR] = RunBeat.Outcome,
        [CommandNames.InitCommit] = RunBeat.Outcome,
        [CommandNames.PrCrossLink] = RunBeat.Outcome,
        [CommandNames.InitComplete] = RunBeat.Outcome,
        [CommandNames.CompletePullRequests] = RunBeat.Outcome, [CommandNames.RefreshBranchBase] = RunBeat.Outcome, // 023fa, 023fb
        [CommandNames.CleanupWorkBranches] = RunBeat.Outcome, // 2026-11-17-713ec
        [CommandNames.PersistWorkBranch] = RunBeat.Outcome,
        [CommandNames.CollectSpecDialogReply] = RunBeat.Outcome,
        [CommandNames.DeliverOutput] = RunBeat.Outcome,
        [CommandNames.DeliverFindings] = RunBeat.Outcome,
        [CommandNames.PostPrComments] = RunBeat.Outcome,
        [CommandNames.SecuritySnapshotWrite] = RunBeat.Outcome,
        [CommandNames.WriteTickets] = RunBeat.Outcome,
        [CommandNames.PlanFiling] = RunBeat.Plan, // 2026-11-17-5701b: the filing run's two steps
        [CommandNames.FileTickets] = RunBeat.Outcome,
        [CommandNames.CloseTicket] = RunBeat.Outcome, // 2026-11-17-e061a
        [CommandNames.ApplyConfigChanges] = RunBeat.Outcome, // 2026-11-17-f3a8c
        [CommandNames.ReopenTicket] = RunBeat.Outcome, [CommandNames.StartTicket] = RunBeat.Outcome,
        [CommandNames.AmendTicket] = RunBeat.Outcome, [CommandNames.PostTicketComments] = RunBeat.Outcome, // e061c
        [CommandNames.RenderTicketBody] = RunBeat.Outcome, // 2026-11-17-dd05b
        [CommandNames.RetargetPullRequests] = RunBeat.Outcome, // 2026-11-17-9a39c
    };
}
