namespace AgentSmith.Contracts.Providers;

/// <summary>
/// 2026-11-18-3863e: which pipeline to start, on which ref, with which inputs. <paramref name="Pipeline"/> is the GitHub
/// workflow file (or id) or the Azure DevOps pipeline's name (folder\name); GitLab runs the project's own pipeline and
/// takes none. <paramref name="Ref"/> null means the repository's default branch.
/// </summary>
public sealed record PipelineRunOrder(string? Pipeline, string? Ref, IReadOnlyDictionary<string, string> Inputs);
