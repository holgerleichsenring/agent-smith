namespace AgentSmith.Contracts.Commands;

public static partial class ContextKeys
{
    /// <summary>p0393a: URL of the draft pull request opened at the spec commit. 2026-10-12-f5f7e:
    /// no longer written — a resume checkpoint written before <see cref="SpecPullRequestUrls"/>
    /// still carries it, and it reads as <c>{SpecRepo: url}</c>.</summary>
    public const string SpecPullRequestUrl = "SpecPullRequestUrl";

    /// <summary>2026-10-12-f5f7e: <c>Dictionary&lt;string, string&gt;</c> — repository → URL of the
    /// draft spec pull request each series repository got at the spec commit (and of an open one a
    /// repository dropped from the series still has), so the run surfaces them even when it later
    /// parks and never reaches CommitAndPR. A concrete dictionary so a checkpoint reads it back.</summary>
    public const string SpecPullRequestUrls = "SpecPullRequestUrls";
}
