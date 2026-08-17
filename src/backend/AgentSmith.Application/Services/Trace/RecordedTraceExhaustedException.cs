namespace AgentSmith.Application.Services.Trace;

/// <summary>
/// p0427: the replay was asked for an answer the recording does not contain.
/// <para>
/// A recording stops where the run stopped, and most recordings worth replaying are of runs
/// that DIED. Serving an invented answer past that point would turn a regression test into
/// fiction that passes for reasons the recording never contained — so the replay says so.
/// The framework already treats a failing model call as a failing model call, which is how
/// an incomplete recording still replays through the run's own finalization.
/// </para>
/// </summary>
public sealed class RecordedTraceExhaustedException(int served)
    : InvalidOperationException(
        $"The recorded run contains {served} answer(s) and the replay was asked for one more. "
        + "The recording ends here — the framework asked for work the run never recorded.")
{
    public int Served { get; } = served;
}
