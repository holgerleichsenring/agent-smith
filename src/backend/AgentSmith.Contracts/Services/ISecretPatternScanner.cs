namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0192: scans text for the small set of credential patterns an agent
/// might leak into a committed file. Used by CommitAndPRHandler as a
/// commit-time gate; pattern set lives in the Infrastructure impl.
/// </summary>
public interface ISecretPatternScanner
{
    IReadOnlyList<SecretMatch> Scan(string path, string content);
}

/// <param name="Path">Source path (informational; e.g. file name or "staged-diff").</param>
/// <param name="Line">1-based line number where the match occurred.</param>
/// <param name="Pattern">Pattern identifier (regex string) that matched.</param>
public sealed record SecretMatch(string Path, int Line, string Pattern);
