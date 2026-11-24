namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-23-c81ee: how a PoC's lifetime is kept, from <c>pocs.lifetime</c> — how often the lifetime pass runs, how many
/// days an extension adds, how many extensions an owner makes before only an admin may, and how long an expired PoC whose
/// owner saw no reminder stays stopped before it is deleted. Absent values read their defaults; the load and the settings
/// store refuse what <see cref="Problems"/> names, never clamping it.
/// </summary>
public sealed class PocLifetimeConfig
{
    public const string Key = "pocs.lifetime";
    public const string IntervalSecondsKey = Key + ".interval_seconds";
    public const string ExtendDaysKey = Key + ".extend_days";
    public const string MaxOwnerExtensionsKey = Key + ".max_owner_extensions";
    public const string UnseenDeleteAfterDaysKey = Key + ".unseen_delete_after_days";

    /// <summary>The seconds between two lifetime passes.</summary>
    public int IntervalSeconds { get; set; } = 3600;

    /// <summary>The days one extension adds to the later of now and the PoC's expiry.</summary>
    public int ExtendDays { get; set; } = 90;

    /// <summary>The extensions an owner makes; past them only a pocs.admin holder extends.</summary>
    public int MaxOwnerExtensions { get; set; } = 2;

    /// <summary>The days after its recorded expiry stop an expired PoC whose owner saw no reminder is deleted.</summary>
    public int UnseenDeleteAfterDays { get; set; } = 30;

    public IReadOnlyList<string> Problems()
    {
        var problems = new List<string>();
        if (IntervalSeconds < 300)
            problems.Add($"{IntervalSecondsKey} is {IntervalSeconds}; it must be 300 or more, because each pass reads every "
                + "PoC, and reminders count in days");
        if (ExtendDays < 1)
            problems.Add($"{ExtendDaysKey} is {ExtendDays}; it must be 1 or more, because an extension that adds no day "
                + "extends nothing");
        if (MaxOwnerExtensions < 0)
            problems.Add($"{MaxOwnerExtensionsKey} is {MaxOwnerExtensions}; it must be 0 or more, because it counts the "
                + "extensions an owner may make");
        if (UnseenDeleteAfterDays < 1)
            problems.Add($"{UnseenDeleteAfterDaysKey} is {UnseenDeleteAfterDays}; it must be 1 or more, because 0 would "
                + "delete an expired PoC in the pass after its stop, before its owner could see the banner and extend it");
        return problems;
    }
}
