using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0439: where each sandbox stood when a phase was verified — the commit, provided the
/// source tree was clean. A shortfall delivers exactly that state and nothing beyond it,
/// so the state has to be named the moment it is verified.
/// <para>
/// A dirty tree at verification time (a checkpoint push that the secret scan refused,
/// say) means the verified work is not on any commit; no head is recorded for that
/// sandbox, and the run cannot deliver a shortfall for it. Run-record paths are not
/// source and do not count.
/// </para>
/// </summary>
public sealed class VerifiedHeads(ILogger<VerifiedHeads> logger)
{
    private const int GitTimeoutSeconds = 120;

    public async Task RecordAsync(
        PipelineContext pipeline, IReadOnlyDictionary<string, ISandbox> sandboxes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(sandboxes);
        var heads = new Dictionary<string, string>(Current(pipeline), StringComparer.Ordinal);
        foreach (var (key, sandbox) in sandboxes)
        {
            var head = await VerifiedHeadAsync(key, sandbox, cancellationToken);
            if (head is null) heads.Remove(key);
            else heads[key] = head;
        }
        pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.VerifiedHeads, heads);
    }

    /// <summary>The verified commit of one sandbox, or null when none was recorded.</summary>
    public static string? For(PipelineContext pipeline, string sandboxKey)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return Current(pipeline).TryGetValue(sandboxKey, out var head) ? head : null;
    }

    private static IReadOnlyDictionary<string, string> Current(PipelineContext pipeline) =>
        pipeline.TryGet<IReadOnlyDictionary<string, string>>(ContextKeys.VerifiedHeads, out var heads)
        && heads is not null
            ? heads
            : new Dictionary<string, string>(StringComparer.Ordinal);

    private async Task<string?> VerifiedHeadAsync(string key, ISandbox sandbox, CancellationToken ct)
    {
        var dirty = await RunAsync(sandbox, ["status", "--porcelain", "--untracked-files=all"], ct);
        if (dirty is null) return null;
        var source = dirty.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(PathOfStatusLine)
            .Where(path => !RunRecordPaths.IsRunRecordPath(path))
            .ToList();
        if (source.Count > 0)
        {
            logger.LogInformation(
                "{Key}: verified with {N} uncommitted source path(s) — no verified head recorded", key, source.Count);
            return null;
        }
        var head = await RunAsync(sandbox, ["rev-parse", "HEAD"], ct);
        return string.IsNullOrWhiteSpace(head) ? null : head.Trim();
    }

    // Porcelain v1: two status columns, a space, the path; a rename reads "old -> new".
    private static string PathOfStatusLine(string line)
    {
        var path = line.Length > 3 ? line[3..].Trim() : line.Trim();
        var arrow = path.IndexOf(" -> ", StringComparison.Ordinal);
        return arrow < 0 ? path : path[(arrow + 4)..];
    }

    private static async Task<string?> RunAsync(ISandbox sandbox, IReadOnlyList<string> args, CancellationToken ct)
    {
        var result = await sandbox.RunStepAsync(
            new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
                Command: "git", Args: args, WorkingDirectory: "/work", TimeoutSeconds: GitTimeoutSeconds),
            progress: null, ct);
        return result.ExitCode == 0 ? result.OutputContent ?? string.Empty : null;
    }
}
