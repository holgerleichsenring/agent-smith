namespace AgentSmith.Contracts.Runs;

/// <summary>
/// 2026-08-25-e257: a run's acceptance dispositions and the human judgements of them, served
/// together.
/// <para>
/// Together because a page that fetches them apart has a window in which it shows a verdict
/// whose correction has not arrived — and the whole point of the correction is that the
/// verdict on its own was misleading.
/// </para>
/// </summary>
public sealed record JudgedAcceptance(
    AcceptanceView? Acceptance,
    IReadOnlyList<CriterionJudgement> Judgements);
