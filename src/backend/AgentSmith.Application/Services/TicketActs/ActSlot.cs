namespace AgentSmith.Application.Services.TicketActs;

/// <summary>
/// 2026-11-17-e061a: the mutable holder the server seeds under ContextKeys.ActOutcome, because the pipeline context
/// never leaves the run; the act's step fills it with what it did. Unset when the run ended before the act.
/// The filing run's <see cref="Filing.FilingReportSlot"/> is the same holder for a filing report.
/// </summary>
public sealed class ActSlot<T>
{
    public T? Value { get; private set; }

    public bool IsSet { get; private set; }

    public void Set(T value) => (Value, IsSet) = (value, true);
}
