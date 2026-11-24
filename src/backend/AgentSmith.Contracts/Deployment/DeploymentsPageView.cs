namespace AgentSmith.Contracts.Deployment;

/// <summary>
/// 2026-11-23-8df6a: what the Deployments page shows for a window — the merged pull requests, newest merge first, and
/// the deploy runs agent smith made, newest delivery first.
/// </summary>
public sealed record DeploymentsPageView(IReadOnlyList<MergedPullRequestItem> Merged, IReadOnlyList<DeployRecordView> Deploys);
