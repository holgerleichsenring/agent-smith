namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-09-17-0e79c: what the check of one phase's premises came to.
/// <para>
/// <see cref="False"/> holds the premises proven false on a look this check took and that RAN —
/// those stop the phase. <see cref="Unproven"/> holds everything the checker reported that the
/// framework could not verify: no citation, an id nobody minted, a look that could not run, or a
/// premise the phase never stated. They are recorded and the phase goes on, because a broken
/// read proves nothing in either direction and an invented premise proves less.
/// </para>
/// <para>
/// The two "nothing was asked" cases are told apart on purpose. <see cref="Skipped"/> is a
/// DECISION not to ask; <see cref="NotTaken"/> is a call that was made or attempted and produced
/// no answer. Both let the phase run, and an operator reading a run must be able to tell "we
/// chose not to ask" from "we paid for a call and could not read what came back".
/// </para>
/// </summary>
public sealed record PremiseCheck(
    IReadOnlyList<PremiseFinding> False,
    IReadOnlyList<PremiseFinding> Unproven,
    string? Skipped = null,
    string? NotTaken = null)
{
    /// <summary>Every premise the phase states still holds.</summary>
    public static PremiseCheck Held { get; } = new([], []);

    /// <summary>No question was asked, and this is the reason an operator reads.</summary>
    public static PremiseCheck Because(string reason) => new([], [], reason);

    /// <summary>The question was put and no answer came back. The phase runs.</summary>
    public static PremiseCheck CouldNotBeTaken(string reason) => new([], [], null, reason);
}
