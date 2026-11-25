namespace AgentSmith.Contracts.Series;

/// <summary>2026-11-25-9992e: what the series master's ci tool does — read the CI on the pull requests' heads, queue their
/// not-started builds again, or start their pull-request CI.</summary>
public static class SeriesCiActs
{
    public const string Read = "read";
    public const string Requeue = "requeue";
    public const string Start = "start";

    public static string Unknown(string act) => $"'{act}' is no CI act; the acts are {Read}, {Requeue} and {Start}.";
}
