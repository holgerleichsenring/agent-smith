using AgentSmith.Contracts.Decisions;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Appends decisions to .agentsmith/decisions.md in the target repository.
/// Thread-safe via SemaphoreSlim for concurrent pipeline runs.
/// </summary>
public sealed class FileDecisionLogger(ILogger<FileDecisionLogger> logger) : IDecisionLogger
{
    private const string AgentSmithDir = ".agentsmith";
    private const string DecisionsFileName = "decisions.md";
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task LogAsync(string? repoPath, DecisionCategory category,
                               string decision, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(repoPath))
        {
            logger.LogDebug("No repo path provided, skipping file write for [{Category}]: {Decision}",
                category, decision);
            return;
        }

        var decisionsPath = Path.Combine(repoPath, AgentSmithDir, DecisionsFileName);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var content = File.Exists(decisionsPath)
                ? await File.ReadAllTextAsync(decisionsPath, cancellationToken)
                : "# Decision Log\n";

            var sectionHeader = $"## {category}";
            var line = $"- {decision}";

            content = content.Contains(sectionHeader)
                ? InsertUnderSection(content, sectionHeader, line)
                : content + $"\n{sectionHeader}\n{line}\n";

            var directory = Path.GetDirectoryName(decisionsPath);
            if (directory is not null && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            await File.WriteAllTextAsync(decisionsPath, content, cancellationToken);
            logger.LogDebug("Logged decision [{Category}]: {Decision}", category, decision);
        }
        finally
        {
            _lock.Release();
        }
    }

    internal static string InsertUnderSection(string content, string header, string line)
    {
        var headerIndex = content.IndexOf(header, StringComparison.Ordinal);
        var afterHeader = headerIndex + header.Length;

        var nextSection = content.IndexOf("\n## ", afterHeader, StringComparison.Ordinal);
        var insertAt = nextSection >= 0 ? nextSection : content.Length;

        var prefix = content[..insertAt].TrimEnd('\n') + "\n";
        var suffix = content[insertAt..];

        return prefix + line + "\n" + suffix;
    }
}
