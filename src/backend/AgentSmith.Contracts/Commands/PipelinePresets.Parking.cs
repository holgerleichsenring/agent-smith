namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    /// <summary>
    /// p0391: true when the preset can PARK a run on an operator question — it carries a
    /// clarification step (SpecHandback and/or MasterOpenQuestions) that posts the
    /// question as an anchored ticket comment AND moves the ticket into
    /// needs_clarification_status, so the run ends and the operator's answer re-triggers it.
    /// Derived from the preset's own command list, never from a hand-maintained name set — a
    /// preset that loses its clarification step loses the capability in the same edit.
    /// <para>
    /// p0393a: SpecHandback parks the same way and counts the same. A run that cannot park
    /// must not hand back silently. p0394a: PlanOpenQuestions left with GeneratePlan —
    /// clarification lives at DERIVATION (SpecHandback) and mid-run (MasterOpenQuestions).
    /// </para>
    /// </summary>
    public static bool ParksOpenQuestions(string pipelineName) =>
        HasStep(pipelineName, CommandNames.MasterOpenQuestions)
        || HasStep(pipelineName, CommandNames.SpecHandback);

    /// <summary>
    /// p0391: true when the preset can park a question the MASTER raised MID-RUN (it runs
    /// MasterOpenQuestions after AgenticMaster). This is the condition for handing the master
    /// the ticket-parking ask_human instead of the live dialogue transport: a ticket-triggered
    /// run has no dialogue job id, so the transport tool only ever answers
    /// "Dialogue transport not configured" and the master is left with no way out.
    /// </summary>
    public static bool ParksMasterQuestions(string pipelineName) =>
        HasStep(pipelineName, CommandNames.MasterOpenQuestions);

    /// <summary>
    /// p0393a: the steps PhaseSequence splices per derived phase. They belong to the preset
    /// even though they are not in its literal list — the capability questions above are
    /// asked at CONFIGURATION time, long before a run splices anything, and answering them
    /// from the literal list alone would report a pipeline that parks mid-run as one that
    /// cannot park at all. That is exactly the silent degradation p0391 exists to prevent.
    /// </summary>
    // p0394a: no plan step — the ratified phase spec IS the plan. The ledger seeds
    // from the spec's steps and the master's plan section renders the spec, so the
    // block goes straight from selecting the phase to executing it.
    public static readonly IReadOnlyList<string> CodePhaseBlock =
    [
        CommandNames.SelectPhase,
        CommandNames.AgenticMaster,
        CommandNames.MasterOpenQuestions,
        CommandNames.VerifyPhase,
        CommandNames.WritePhaseRecord,
    ];

    /// <summary>
    /// p0393a: the preset's command list with PhaseSequence expanded into the block it
    /// splices. Everything that REASONS about a preset — capability checks, the run-rail
    /// beats, the data-flow contract — needs the steps a run will actually execute; only
    /// the EXECUTOR wants the literal list, because the number of blocks is not known
    /// until the specs are derived.
    /// </summary>
    public static IReadOnlyList<string> Effective(string pipelineName)
    {
        var commands = TryResolve(pipelineName);
        if (commands is null) return [];
        if (!commands.Contains(CommandNames.PhaseSequence, StringComparer.Ordinal))
            return commands;
        return [.. commands.SelectMany(
            c => c == CommandNames.PhaseSequence ? CodePhaseBlock : [c])];
    }

    private static bool HasStep(string pipelineName, string commandName) =>
        Effective(pipelineName).Contains(commandName, StringComparer.Ordinal);
}
