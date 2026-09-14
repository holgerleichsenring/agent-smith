namespace AgentSmith.Contracts.Runs;

/// <summary>
/// 2026-09-06-3d81: a criterion the agent declined — one no work in this repository could
/// make true — with the evaluated meaning of not doing it, as the run detail serves it on
/// <see cref="AcceptanceView.Declined"/>.
/// </summary>
/// <param name="Phase">The phase whose master declined it; null on a run judged by a
/// negotiated expectation rather than a phase spec.</param>
public sealed record DeclinedCriterionView(string Text, string Reason, string? Phase = null);
