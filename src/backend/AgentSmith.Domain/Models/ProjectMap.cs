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
    CiConfig Ci,
    // p0202e: the analyzer-DERIVED command that prepares the environment before
    // tests, chosen from what is actually committed (e.g. "npm install" — or
    // "npm ci" ONLY with a committed package-lock.json; "go mod download"; null
    // for .NET). Top-level (not a CI concern); the EnsurePrerequisites step uses
    // it unless the operator set a context.yaml `prerequisites` override.
    string? Prerequisites = null);
