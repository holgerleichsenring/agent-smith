namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0504: one command the verify gate will run for a repository, and the working
/// directory it runs in. Produced by <see cref="VerifyStageResolver"/> — declared by
/// the context, inferred by the analyzer or discovered from the tree — and consumed by
/// VerifyCommandRunner.
/// </summary>
public sealed record VerifyStage(string Stage, string Command, string Cwd);
