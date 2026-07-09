namespace AgentSmith.Contracts.Commands;

public static partial class ContextKeys
{
    /// <summary>p0315d: the validated phase spec (PhaseDraft) extracted from the
    /// phase ticket by PhaseSpecGate — the requirement record the master
    /// executes and the artifact WritePhaseRecord commits to phases/done/.</summary>
    public const string PhaseSpec = "PhaseSpec";

    /// <summary>p0315d: questions the master raised mid-run via ask_human on a
    /// ticket-triggered run (IReadOnlyList&lt;PlanOpenQuestion&gt;) — captured by
    /// TicketClarificationToolHost, posted + parked by MasterOpenQuestions.</summary>
    public const string MasterOpenQuestions = "MasterOpenQuestions";
}
