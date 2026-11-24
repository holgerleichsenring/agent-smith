namespace AgentSmith.Contracts.Providers;

/// <summary>
/// 2026-11-23-933dd: one file of a run's artifact — the artifact's <paramref name="Name"/>, the <paramref name="Job"/>
/// that uploads it (GitLab keeps artifacts per job, so it reads that job's; GitHub and Azure DevOps read the run's
/// artifact by name), and the <paramref name="File"/> inside it.
/// </summary>
public sealed record PipelineArtifact(string Name, string Job, string File);
