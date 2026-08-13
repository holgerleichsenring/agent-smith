using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// p0411: the paths a run has changed so far, read from the sandboxes by the
/// framework and carried in the master's working state, so the changed-file
/// question is answered where the master already looks instead of costing it a
/// `git status` / `git diff` round trip to re-derive.
/// <para>
/// Paths come back repo-prefixed exactly as the master addresses files when more
/// than one repo is in scope. Never throws — a sandbox that cannot be read
/// contributes nothing rather than failing the pass.
/// </para>
/// </summary>
public sealed class SandboxWorkingTreeReader(ILogger<SandboxWorkingTreeReader> logger)
{
    private const int TimeoutSeconds = 60;

    public async Task<IReadOnlyList<string>> ChangedPathsAsync(
        IReadOnlyDictionary<string, ISandbox> sandboxes,
        IReadOnlyDictionary<string, string>? keyToRepo,
        CancellationToken cancellationToken)
    {
        var repoNames = keyToRepo?.Values.Distinct(StringComparer.Ordinal).ToList() ?? [];
        var prefixed = repoNames.Count > 1;
        var paths = new List<string>();
        foreach (var (key, sandbox) in sandboxes)
        {
            var repo = keyToRepo is not null && keyToRepo.TryGetValue(key, out var name) ? name : key;
            var prefix = prefixed && !string.IsNullOrEmpty(repo) ? repo + "/" : string.Empty;
            foreach (var path in await ReadOneAsync(sandbox, cancellationToken))
                paths.Add(prefix + path);
        }
        return [.. paths.Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal)];
    }

    private async Task<IReadOnlyList<string>> ReadOneAsync(ISandbox sandbox, CancellationToken ct)
    {
        try
        {
            var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
                Command: "git", Args: ["status", "--porcelain"], TimeoutSeconds: TimeoutSeconds);
            var result = await sandbox.RunStepAsync(step, null, ct);
            if (result.ExitCode != 0) return [];
            return ParsePorcelain(result.OutputContent ?? string.Empty);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogDebug(ex, "Working-tree read failed — the state block omits this sandbox");
            return [];
        }
    }

    // Porcelain v1: two status columns, a space, then the path. A rename carries
    // "old -> new"; the NEW path is the one the master can open.
    internal static IReadOnlyList<string> ParsePorcelain(string output) =>
        [.. output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Length > 3 ? line[3..].Trim() : string.Empty)
            .Select(path => path.Contains(" -> ", StringComparison.Ordinal)
                ? path[(path.IndexOf(" -> ", StringComparison.Ordinal) + 4)..]
                : path)
            .Select(path => path.Trim('"'))
            .Where(path => path.Length > 0)];
}
