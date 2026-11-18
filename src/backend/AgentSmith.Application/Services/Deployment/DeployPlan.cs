namespace AgentSmith.Application.Services.Deployment;

/// <summary>2026-11-18-3863c: what a deploy does — the sentence that heads its outcome, and the manifest change to
/// deliver; no order when the environment already carries the version.</summary>
public sealed record DeployPlan(string Headline, GitOpsOrder? Order);
