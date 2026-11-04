namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-11-02-2b26c: a done entry and a goal state the code AFTER the work. Every reader that judges a
/// spec or a ticket against the repositories as they are today is told so in this one sentence — the
/// premise check carried it alone, and the attack review, the cut review, the contradiction check and
/// the derivation each read "the code does not do this yet" as a refusal or a contradiction.
/// <para>
/// It never says "criterion": the cut review's verdict list must not assume a spec states criteria.
/// </para>
/// </summary>
public static class DoneTargetState
{
    public const string Rule =
        "That the code does not meet a done entry or the goal yet is never by itself a finding, a premise "
        + "or a contradiction: they state what holds once the work is done, and the gap to them is what the work is FOR.";
}
