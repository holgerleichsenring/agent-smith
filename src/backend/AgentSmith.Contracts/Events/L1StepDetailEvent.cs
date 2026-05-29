namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0173e: typed detail row for an in-flight pipeline step. Replaces
/// <c>IProgressReporter.ReportDetailAsync(string)</c>, which carried a
/// free-form blob that the dashboard rendered as repeated identical rows.
/// <see cref="StepIndex"/> binds the detail to its enclosing step so the
/// dashboard groups them; <see cref="Origin"/> identifies the producer
/// (skill name, tool name, batch slot, …) so multi-source detail streams
/// stay distinguishable. <see cref="Detail"/> is the operator-readable
/// body — typed by the channel, not free-form across the surface.
/// </summary>
public sealed record L1StepDetailEvent(
    string RunId,
    int StepIndex,
    string Origin,
    string Detail,
    DateTimeOffset Timestamp)
    : RunEvent(RunId, EventType.L1StepDetail, Timestamp);
