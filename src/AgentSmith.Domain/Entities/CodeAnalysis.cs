namespace AgentSmith.Domain.Entities;

/// <summary>
/// Result of analyzing a repository's structure and dependencies.
/// </summary>
public sealed class CodeAnalysis
{
    public IReadOnlyList<string> FileStructure { get; }
    public IReadOnlyList<string> Dependencies { get; }
    public string? Framework { get; }
    public string? Language { get; }

    public CodeAnalysis(
        IReadOnlyList<string> fileStructure,
        IReadOnlyList<string> dependencies,
        string? framework,
        string? language)
    {
        FileStructure = fileStructure;
        Dependencies = dependencies;
        Framework = framework;
        Language = language;
    }
}
