namespace AgentSmith.Contracts.Dialogue;

/// <summary>What one <see cref="SeriesEdit"/> does to the series.</summary>
public static class SeriesEditKinds
{
    /// <summary>Replace a spec's text; the yaml's spec field must name the spec.</summary>
    public const string Recut = "recut";

    /// <summary>Remove a spec that has not run.</summary>
    public const string Drop = "drop";

    /// <summary>Add a spec at the end; its id is minted anew under the series.</summary>
    public const string Append = "append";
}
