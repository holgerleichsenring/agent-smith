namespace AgentSmith.Contracts.Services;

/// <summary>
/// Detects whether a repository uses a specific language/runtime stack.
/// Returns null if the language is not detected. Multiple detectors are
/// tried in order; the first non-null result wins.
/// </summary>
public interface ILanguageDetector
{
    LanguageDetectionResult? Detect(string repoPath);
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
