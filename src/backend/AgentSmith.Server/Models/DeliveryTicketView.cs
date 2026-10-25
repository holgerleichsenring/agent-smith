namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-25-0099b: one ticket worked in the period, with its attempts oldest first.
/// </summary>
/// <param name="Bucket">The bucket the ticket list shows it in; null when the list did not reach it.</param>
/// <param name="SpecTitles">The newest attempt's spec goals, in order.</param>
/// <param name="Repos">The repositories the newest attempt changed.</param>
/// <param name="WallSeconds">From the first attempt's start to the merge, else to the last activity.</param>
public sealed record DeliveryTicketView(
    string Project,
    string TicketId,
    string? Title,
    string? Bucket,
    DateTimeOffset? MergedAt,
    IReadOnlyList<DeliveryAttemptView> Attempts,
    int SpecsDone,
    int SpecsTotal,
    IReadOnlyList<string> SpecTitles,
    IReadOnlyList<string> Repos,
    double AgentSeconds,
    double WallSeconds,
    decimal CostUsd,
    DateTimeOffset LastActivityAt);
