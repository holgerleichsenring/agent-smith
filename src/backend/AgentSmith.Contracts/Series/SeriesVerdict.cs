namespace AgentSmith.Contracts.Series;

/// <summary>2026-11-17-0207d: one verify run's check of the series' done criteria on the default branch.</summary>
public sealed record SeriesVerdict(string RunId, DateTimeOffset At, IReadOnlyList<SeriesCriterionResult> Results)
{
    /// <summary>The follow-up ticket the failures were filed as; null when none was filed.</summary>
    public string? FollowUpTicketId { get; init; }

    /// <summary>What became of the follow-up's start, said when it did not start.</summary>
    public string? FollowUpStart { get; init; }

    /// <summary>The tickets whose specs could not be read off their branch, with why.</summary>
    public IReadOnlyList<string> Unread { get; init; } = [];

    /// <summary>True once a beat reported it into the conversation.</summary>
    public bool Reported { get; init; }

    /// <summary>The criteria checked and found not to hold. 2026-11-28-d1ffa: a criterion that could not be checked here is none
    /// of them.</summary>
    public IReadOnlyList<SeriesCriterionResult> Failed => [.. Results.Where(r => r is { Passed: false, NotCheckable: null })];

    /// <summary>2026-11-28-d1ffa: the criteria the check's environment kept from being checked, each with why.</summary>
    public IReadOnlyList<SeriesCriterionResult> NotCheckable => [.. Results.Where(r => r.NotCheckable is not null)];
}
