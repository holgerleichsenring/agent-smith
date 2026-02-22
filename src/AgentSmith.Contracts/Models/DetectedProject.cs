namespace AgentSmith.Contracts.Models;

/// <summary>
/// Result of deterministic project detection. Contains language, runtime,
/// package manager, build/test commands, frameworks, and key files to read.
/// </summary>
public sealed record DetectedProject(
    string Language,
    string? Runtime,
    string? PackageManager,
    string? BuildCommand,
    string? TestCommand,
    IReadOnlyList<string> Frameworks,
    IReadOnlyList<string> Infrastructure,
    IReadOnlyList<string> KeyFiles,
    IReadOnlyList<string> Sdks,
    string? ReadmeExcerpt = null);
