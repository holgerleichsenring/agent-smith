namespace AgentSmith.Contracts.Runs;

/// <summary>2026-10-13-9a59d: the kinds of <see cref="AttemptSource"/>.</summary>
public static class AttemptSourceKinds
{
    /// <summary>A keyword comment on the ticket.</summary>
    public const string Comment = "comment";

    /// <summary>A standing Request changes on a pull request.</summary>
    public const string Review = "review";

    /// <summary>A trusted comment command on a pull request.</summary>
    public const string Command = "command";

    /// <summary>A person's move of the ticket into a trigger status.</summary>
    public const string Status = "status";
}
