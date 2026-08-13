namespace AgentSmith.Contracts.Runs;

/// <summary>
/// p0405: one step the run has NOT reached yet, as the executor's live command
/// list already knows it — its position, its typed command, the label it will
/// carry, and the spliced phase (p0393a) it belongs to.
/// <para>
/// Deliberately carries no status, no cost and no duration: an unreached step has
/// none, and a skeleton that borrowed the vocabulary of a finished step would
/// invite the same confusion in the other direction.
/// </para>
/// </summary>
public sealed record PlannedStepView(
    int StepIndex,
    string CommandName,
    string DisplayName,
    string? PhaseId);
