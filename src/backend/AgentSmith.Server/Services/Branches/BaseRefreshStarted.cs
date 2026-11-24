namespace AgentSmith.Server.Services.Branches;

/// <summary>2026-11-24-9dc0e: a refresh-base start's answer — the run it started, or the sentence why none.</summary>
public sealed record BaseRefreshStarted(string? RunId, string? Refusal)
{
    public static BaseRefreshStarted Started(string runId) => new(runId, null);

    public static BaseRefreshStarted Refused(string sentence) => new(null, sentence);
}
