namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    /// <summary>
    /// p0391: true when the preset can PARK a run on an operator question — it carries a
    /// clarification step (PlanOpenQuestions and/or MasterOpenQuestions) that posts the
    /// question as an anchored ticket comment AND moves the ticket into
    /// needs_clarification_status, so the run ends and the operator's answer re-triggers it.
    /// Derived from the preset's own command list, never from a hand-maintained name set — a
    /// preset that loses its clarification step loses the capability in the same edit.
    /// </summary>
    public static bool ParksOpenQuestions(string pipelineName) =>
        HasStep(pipelineName, CommandNames.PlanOpenQuestions)
        || HasStep(pipelineName, CommandNames.MasterOpenQuestions);

    /// <summary>
    /// p0391: true when the preset can park a question the MASTER raised MID-RUN (it runs
    /// MasterOpenQuestions after AgenticMaster). This is the condition for handing the master
    /// the ticket-parking ask_human instead of the live dialogue transport: a ticket-triggered
    /// run has no dialogue job id, so the transport tool only ever answers
    /// "Dialogue transport not configured" and the master is left with no way out.
    /// </summary>
    public static bool ParksMasterQuestions(string pipelineName) =>
        HasStep(pipelineName, CommandNames.MasterOpenQuestions);

    private static bool HasStep(string pipelineName, string commandName) =>
        TryResolve(pipelineName)?.Contains(commandName, StringComparer.Ordinal) == true;
}
