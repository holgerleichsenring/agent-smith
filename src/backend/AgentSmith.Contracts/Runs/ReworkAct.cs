namespace AgentSmith.Contracts.Runs;

/// <summary>
/// 2026-10-08-e8b9b: a person's deliberate request to rework a finished ticket — who made it and
/// when, by the HOST's clock (the comment's or review's own time, never the moment a webhook was
/// received, so a redelivery is recognised as the act it already was).
/// <para>2026-10-08-f114: <see cref="Channel"/> says where the act was made; only a pull-request act
/// is a revision cause of its own — on the ticket, Comment and RecutDemand already are.</para>
/// </summary>
public sealed record ReworkAct(string Author, DateTimeOffset At, ReworkChannel Channel = ReworkChannel.Ticket);
