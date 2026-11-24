namespace AgentSmith.Application.Services.BaseRefresh;

/// <summary>2026-11-24-6811c: what a refresh made of the merge — push it (it was checked), leave the branch with a plain
/// word (already level, no base to read), or refuse with the reason a person reads; a refused branch is back at its head.</summary>
public sealed record BaseRefreshOutcome(BaseRefreshVerdict Verdict, string Text)
{
    public static BaseRefreshOutcome Push(string text) => new(BaseRefreshVerdict.Push, text);

    public static BaseRefreshOutcome Plain(string text) => new(BaseRefreshVerdict.Plain, text);

    public static BaseRefreshOutcome Refuse(string reason) => new(BaseRefreshVerdict.Refuse, reason);
}

/// <summary>2026-11-24-6811c: the three ends of a refresh.</summary>
public enum BaseRefreshVerdict
{
    Push,
    Plain,
    Refuse,
}
