namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>2026-11-17-9a39a: the configuration spelling of <see cref="RequiresStart"/> — <c>requires_start:</c>.</summary>
public static class RequiresStartNames
{
    public const string OnGreenPush = "on_green_push";
    public const string OnMerge = "on_merge";

    /// <summary>The mode a written value names; null or blank is the default, an unknown value is null.</summary>
    public static RequiresStart? Parse(string? written) => written?.Trim().ToLowerInvariant() switch
    {
        null or "" or OnGreenPush => RequiresStart.OnGreenPush,
        OnMerge => RequiresStart.OnMerge,
        _ => null,
    };

    /// <summary>The value as written, the default's name when nothing is.</summary>
    public static string Written(string? written) => string.IsNullOrWhiteSpace(written) ? OnGreenPush : written.Trim();
}
