namespace AgentSmith.Server.Services.Webhooks;

/// <summary>What a run needs to know about the pull request it was started for, read out of
/// whichever host's payload announced it. Absent values are null, never guessed.</summary>
public sealed record PullRequestFacts(
    string Number, string? HeadSha, string? BaseSha, string? Author, string? HeadBranch);
