namespace AgentSmith.Contracts.Commands;

/// <summary>2026-11-17-9a39c: the step of the 'retarget' run.</summary>
public static partial class CommandNames
{
    /// <summary>Moves a stacked dependent's open pull requests off a landed prerequisite's branch.</summary>
    public const string RetargetPullRequests = "RetargetPullRequestsCommand";
}
