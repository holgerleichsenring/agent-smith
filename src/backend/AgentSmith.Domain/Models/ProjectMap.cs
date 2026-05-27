namespace AgentSmith.Domain.Models;

/// <summary>
/// Structured representation of a repository discovered by ProjectAnalyzer.
/// Replaces the heuristic CodeAnalysis + free-form code-map.yaml. Persisted
/// to <c>.agentsmith/project-map.json</c> in the analyzed repo, cache-keyed
/// by dependency-manifest content hash.
/// </summary>
public sealed record ProjectMap(
    string PrimaryLanguage,
    IReadOnlyList<string> Frameworks,
    IReadOnlyList<Module> Modules,
    IReadOnlyList<TestProject> TestProjects,
    IReadOnlyList<string> EntryPoints,
    Conventions Conventions,
    CiConfig Ci);
