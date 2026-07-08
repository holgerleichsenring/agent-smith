namespace AgentSmith.Server.Services.Webhooks;

/// <summary>One resolved pr-event route: the matched project, the project repo
/// the PR belongs to, and the pipeline the event dispatches (p0167a).</summary>
public sealed record PrReviewRoute(string ProjectName, string RepoName, string PipelineName);
