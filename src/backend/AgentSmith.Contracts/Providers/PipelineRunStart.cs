namespace AgentSmith.Contracts.Providers;

/// <summary>
/// 2026-11-18-3863e: a started pipeline run — its id on the host and its page. <paramref name="RunId"/> is null when the
/// host started the run without naming it (a GitHub Enterprise Server answering 204); <paramref name="Url"/> is then the
/// workflow's runs page.
/// </summary>
public sealed record PipelineRunStart(string? RunId, string Url);
