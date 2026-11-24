namespace AgentSmith.Contracts.Reviews;

/// <summary>2026-11-24-f9c5d: the answer to starting a pull request's CI — the outcome, the sentence a person reads on the
/// series page, and the host's link to what started when it names one.</summary>
public sealed record CiTrigger(CiTriggerOutcome Outcome, string Text, string? Url = null)
{
    public static CiTrigger Started(string text, string? url = null) => new(CiTriggerOutcome.Started, text, url);

    public static CiTrigger None(string text) => new(CiTriggerOutcome.NoCi, text);

    public static CiTrigger Impossible(string text) => new(CiTriggerOutcome.Impossible, text);
}
