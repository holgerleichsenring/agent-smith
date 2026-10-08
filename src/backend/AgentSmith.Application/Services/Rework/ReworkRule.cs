namespace AgentSmith.Application.Services.Rework;

/// <summary>2026-10-08-e8b9c: the one sentence both pull-request bodies carry, so a reviewer knows
/// what their review does before they write it.</summary>
public static class ReworkRule
{
    public const string PrBodyLine =
        "_Review comments on this pull request are collected for the next attempt; a rework starts only "
        + "when a reviewer chooses **Request changes** (Azure DevOps: vote **Wait for author**)._";
}
