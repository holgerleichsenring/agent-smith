namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-08-28-5f71: what each repository's branch delivers, and what it means when
/// nothing verified it.
/// <para>
/// Three answers, not two. A delivery diff that FAILED — no comparable base ref, which
/// is the shallow clone and the freshly-onboarded repository — carries empty text, and
/// reading empty as "delivered nothing" sent exactly those repositories through the
/// branch that passes without checking anything. Undetermined is not unchanged.
/// </para>
/// <para>
/// The run-level answer is what this type exists for. Per repository the skip stays
/// right: not every repository in a multi-repo run is buildable, and one that declares
/// nothing is skipped rather than failed. But a run in which NO repository ran a command
/// over a delivery has had one party's word and no second opinion, and it stops being
/// reported as a success.
/// </para>
/// </summary>
public sealed class DeliveredWork
{
    private readonly IReadOnlyDictionary<string, State> _states;

    private DeliveredWork(IReadOnlyDictionary<string, State> states) => _states = states;

    public static DeliveredWork Of(IReadOnlyDictionary<string, DeliveryDiff.DiffResult> diffs)
    {
        ArgumentNullException.ThrowIfNull(diffs);
        return new DeliveredWork(diffs.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.Failed
                ? State.Undetermined
                : DeliveryDiff.CarriesSource(entry.Value.Text) ? State.Source : State.Nothing,
            StringComparer.Ordinal));
    }

    /// <summary>
    /// Has this repository anything a build could be green about? A branch whose diff
    /// could not be taken is included: what it changed is unknown, and unknown is not a
    /// reason to skip the gate.
    /// </summary>
    public bool HasSomethingToProve(string key) =>
        _states.TryGetValue(key, out var state) && state is not State.Nothing;

    /// <summary>True when any repository delivered, or when any could not be read.</summary>
    public bool Anything => _states.Values.Any(state => state is not State.Nothing);

    /// <summary>
    /// Why this run cannot be called verified, or null when it can be. A run that
    /// executed a command has a second opinion; a run that delivered nothing has nothing
    /// to have an opinion about.
    /// </summary>
    public string? Unverified(bool anyCommandRan, IReadOnlyList<string> searched)
    {
        ArgumentNullException.ThrowIfNull(searched);
        if (anyCommandRan || !Anything) return null;
        return "UNVERIFIED: this run executed no verification command over what it delivered."
            + $"\nNothing but the account has looked at what it shipped. {Describe()} "
            + (searched.Count > 0
                ? string.Join(" ", searched)
                : "No repository resolved a command.")
            + " Declare a verify block in the repository's context.yaml (or "
            + "ci.build_command / ci.test_command) so the gate has something to run.";
    }

    private string Describe() =>
        string.Join(" ", _states
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => $"{Named(entry.Key)}: {Word(entry.Value)}."));

    private static string Named(string key) => string.IsNullOrEmpty(key) ? "(default)" : key;

    private static string Word(State state) => state switch
    {
        State.Source => "delivered source",
        State.Undetermined =>
            "delivery undetermined — no comparable base ref, so what this branch changed "
            + "could not be read at all",
        _ => "delivered nothing",
    };

    private enum State
    {
        Nothing,
        Source,
        Undetermined,
    }
}
