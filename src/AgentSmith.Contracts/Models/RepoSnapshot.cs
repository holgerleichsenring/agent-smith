namespace AgentSmith.Contracts.Models;

/// <summary>
/// Raw repository data collected for LLM interpretation during bootstrap.
/// Contains config file contents, code samples, and directory tree — no interpretation, just data.
/// </summary>
public sealed record RepoSnapshot(
    IReadOnlyList<string> ConfigFileContents,
    IReadOnlyList<string> CodeSamples,
    string DirectoryTree);
