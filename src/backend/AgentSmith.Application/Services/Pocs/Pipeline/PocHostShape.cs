namespace AgentSmith.Application.Services.Pocs.Pipeline;

/// <summary>
/// 2026-11-23-933dd: where one host's jobs find and leave their files, and how its jobs differ — the build context
/// after the checkout, the image tar each later job downloads, where the scan and push jobs write what agent smith reads
/// back, how Trivy is run (a pinned container on a hosted agent, the binary in the GitLab job's own image), how the push
/// job signs in, and how the Azure DevOps and GitLab checkouts reach the repository.
/// </summary>
public sealed record PocHostShape(
    string Context, string ImageIn, string ScanOut, string PushOut, string TrivyRun, string Login,
    string RepoUrl, string AuthOn, string AuthOff);
