namespace AgentSmith.Application.Services.Rework;

/// <summary>2026-10-08-e8b9c: the one sentence both pull-request bodies carry, so a reviewer knows
/// what their review does before they write it. The command is quoted mid-line, never at a line's
/// start, so our own text can never read as one.</summary>
public static class ReworkRule
{
    public const string PrBodyLine =
        "_Review comments on this pull request are collected for the next attempt; to rework all of them, add a "
        + "comment `/agent-smith` (further instructions may follow it) or choose **Request changes** "
        + "(Azure DevOps: vote **Wait for author**)._";
}
