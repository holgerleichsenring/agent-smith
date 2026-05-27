namespace AgentSmith.Domain.Models;

/// <summary>
/// A test project discovered in the repo. Skill prompts (Tester role) consume
/// FileCount + SampleTestPath as evidence so coverage discussions are
/// fact-based, not hallucinated.
/// </summary>
public sealed record TestProject(
    string Path,
    string Framework,
    int FileCount,
    string? SampleTestPath);
