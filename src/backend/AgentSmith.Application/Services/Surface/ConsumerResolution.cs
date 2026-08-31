namespace AgentSmith.Application.Services.Surface;

/// <summary>
/// 2026-08-30-c6ec: which configured repositories declared that they consume the interface
/// this run holds a served description of.
/// <para>
/// <paramref name="Unresolvable"/> carries the declared name that matched nothing. It is
/// the one input state that FAILS the run: a difference computed over a subset the
/// operator did not choose reads as a clean bill, and a clean bill nobody earned is worse
/// than no bill at all.
/// </para>
/// </summary>
public sealed record ConsumerResolution(
    IReadOnlyList<string> Repos,
    string? Unresolvable,
    bool AnyDeclared);
