namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-23-c81ea: how a PoC's manifests are committed to the GitOps repository, from the <c>pocs.gitops</c> block.
/// Absent values read their defaults.
/// </summary>
public sealed class PocGitOpsConfig
{
    /// <summary>How many times one commit is written before the run gives up on a branch other commits keep moving.</summary>
    public int CommitAttempts { get; set; } = 3;

    /// <summary>The most files a PoC's deploy folder may hold before the commit refuses to plan their deletion.</summary>
    public int MaxFolderFiles { get; set; } = 500;

    /// <summary>What is wrong with these values, one sentence each naming the key, the value and the reason; empty when
    /// nothing is. The load and the settings store refuse what this names, never clamping it.</summary>
    public IReadOnlyList<string> Problems()
    {
        var problems = new List<string>();
        if (CommitAttempts < 1)
            problems.Add($"pocs.gitops.commit_attempts is {CommitAttempts}; it must be 1 or more, because it counts the "
                + "writes of one commit, and a commit nobody writes deploys nothing");
        if (MaxFolderFiles < 1)
            problems.Add($"pocs.gitops.max_folder_files is {MaxFolderFiles}; it must be 1 or more, because a deploy "
                + "folder is rendered from the app template and holds a handful of manifests; more means files someone "
                + "else put there, and planning their deletion would send every path in one commit request");
        return problems;
    }
}
