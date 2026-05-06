using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Detects whether a repository uses a specific language/runtime stack.
/// Routes filesystem reads through ISandboxFileReader so detection works
/// against the sandbox /work tree. Returns null when the language is not
/// detected. Multiple detectors are tried in order; first non-null wins.
/// </summary>
public interface ILanguageDetector
{
    Task<LanguageDetectionResult?> DetectAsync(
        ISandboxFileReader reader, string repoPath, CancellationToken cancellationToken);
}

/// <summary>
/// Result of a single language detector. Contains language-specific details
/// that get merged with infrastructure/readme data by ProjectDetector.
/// </summary>
public sealed record LanguageDetectionResult(
    string Language,
    string? Runtime,
    string? PackageManager,
    string? BuildCommand,
    string? TestCommand,
    IReadOnlyList<string> Frameworks,
    IReadOnlyList<string> KeyFiles,
    IReadOnlyList<string> Sdks);
