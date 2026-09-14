namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-09-13-a284: what the work-branch switch left behind — the reason the run must
/// stop, or the rung this branch was actually cut from.
/// <para>
/// The switch knew the rung all along and threw it away, because nothing downstream had a
/// use for it. The pull request does: it is opened three steps later, by a site that has
/// no sandbox to ask. Returning the answer instead of re-deriving it is what keeps the
/// base a run CUT from and the base its pull request OPENS against the same fact.
/// </para>
/// </summary>
/// <param name="Problem">Null when the sandbox is ready to be worked in; otherwise the
/// reason the run must stop before anything reads or writes this tree.</param>
/// <param name="Rung">The feature branch this repository's work was cut from, or null
/// when the ladder fell through to the clone's own base — which is not a target anyone
/// chose, and is left to the provider's default-branch resolution.</param>
public sealed record WorkBranchPlacement(string? Problem, string? Rung)
{
    public static WorkBranchPlacement On(string? rung) => new(Problem: null, rung);

    public static WorkBranchPlacement Stop(string problem) => new(problem, Rung: null);
}
