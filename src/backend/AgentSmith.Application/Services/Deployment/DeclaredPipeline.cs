namespace AgentSmith.Application.Services.Deployment;

/// <summary>
/// 2026-11-18-3863e: the pipeline an environment is delivered by — its name on the host (GitHub workflow file or id,
/// Azure DevOps pipeline name; GitLab takes none), the ref it runs on (null: the repository's default branch), and its
/// inputs, whose values may name {version}, {environment}, {context} and the template's parameters.
/// </summary>
public sealed record DeclaredPipeline(string? Name, string? Ref, IReadOnlyDictionary<string, string> Inputs);
