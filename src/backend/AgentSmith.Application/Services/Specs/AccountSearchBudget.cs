namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-08-25-6f12: how many searches one accounting PASS may run, and the fence that stops
/// one pass spending another's allowance.
/// <para>
/// p0483 capped the searches on the account, which is one instance shared by every diff
/// window and by the correction. A live two-repository migration then spent all twelve inside
/// the windowed pass and every later question was answered "No search left" — the refusal it
/// produced said "the required branch-wide absence search could not run", which is true and
/// says nothing about the branch. A bigger shared pool does not fix that: it is still one
/// pool, and the windows still empty it.
/// </para>
/// <para>
/// So the allowance is per PASS. Each pass opens its own before it asks, and a pass that has
/// not opened yet cannot be drawn on by the pass currently running — which is what
/// "capacity the windowed pass cannot consume" has to mean to be worth anything.
/// </para>
/// </summary>
public sealed class AccountSearchBudget
{
    /// <summary>How many searches one pass may run. An account that cannot settle a criterion
    /// in this many looks is not going to, and every search is a sandbox round-trip inside a
    /// model call at the end of a run.</summary>
    public const int PerPass = 12;

    /// <summary>What a search past the allowance is told. Its own text so both search tools
    /// and the pass that fences them read one sentence.</summary>
    public static readonly string Exhausted =
        $"No search left — a pass may run {PerPass}. Decide on what you have.";

    private int _spent;
    private int _ceiling = PerPass;

    /// <summary>Grants the pass that is about to ask a fresh allowance, whatever the passes
    /// before it spent. Raising the ceiling rather than resetting the count keeps the total a
    /// running number, so a log of what an account ran stays readable.</summary>
    public void OpenNextPass() => Interlocked.Exchange(ref _ceiling, Volatile.Read(ref _spent) + PerPass);

    /// <summary>Takes one search from the open allowance, or refuses.</summary>
    public bool TryTake() => Interlocked.Increment(ref _spent) <= Volatile.Read(ref _ceiling);
}
