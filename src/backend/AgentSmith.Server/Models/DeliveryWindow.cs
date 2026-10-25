namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-25-0099b: the period asked for — day, week or month, rolling and ending now — the span before it that
/// the deltas compare with, and the slots the sparklines are drawn from, oldest first.
/// </summary>
public sealed record DeliveryWindow(
    string Period, DeliverySpan Current, DeliverySpan Previous, IReadOnlyList<DeliverySpan> Slots);
