namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// Ergonomic file-IO surface over an ISandbox. Each method wraps the corresponding
/// StepKind (ReadFile / WriteFile / ListFiles) and returns typed results suitable
/// for handler call sites previously using System.IO.File / System.IO.Directory.
/// </summary>
public interface ISandboxFileReader
{
    Task<bool> ExistsAsync(string path, CancellationToken cancellationToken);

    Task<string?> TryReadAsync(string path, CancellationToken cancellationToken);

    Task<string> ReadRequiredAsync(string path, CancellationToken cancellationToken);

    Task WriteAsync(string path, string content, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ListAsync(string path, int? maxDepth, CancellationToken cancellationToken);
}
