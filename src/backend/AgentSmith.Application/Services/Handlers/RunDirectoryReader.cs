using AgentSmith.Application.Services;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Enumerates run directories under <c>.agentsmith/runs/</c>. Post-p0156:
/// directory names match <c>{yyyy-MM-ddTHH-mm-ss}-{4hex}-{slug}</c>; legacy
/// <c>r{NN}-{slug}</c> directories are NOT enumerated (clean cutover). The
/// <c>.last-compiled</c> marker stores the full RunId string, and the fixed
/// canonical format makes ordinal string comparison a valid order operator.
/// </summary>
public static class RunDirectoryReader
{
    private const string LastCompiledFile = ".last-compiled";
    private const string IndexFile = "index.md";

    public static async Task<List<RunDirectoryInfo>> GetRunDirectoriesAsync(
        ISandboxFileReader reader, string runsDir, CancellationToken cancellationToken)
    {
        var entries = await reader.ListAsync(runsDir, maxDepth: 1, cancellationToken);
        var result = new List<RunDirectoryInfo>();
        foreach (var dir in entries)
        {
            var name = LastSegment(dir);
            var runId = TryExtractRunId(name);
            if (runId is not null)
                result.Add(new RunDirectoryInfo(dir, runId, name));
        }
        return result.OrderBy(r => r.RunId, StringComparer.Ordinal).ToList();
    }

    public static async Task<string> ReadLastCompiledAsync(
        ISandboxFileReader reader, string wikiDir, CancellationToken cancellationToken)
    {
        var content = await reader.TryReadAsync(Path.Combine(wikiDir, LastCompiledFile), cancellationToken);
        return content?.Trim() ?? string.Empty;
    }

    public static async Task<string> ReadExistingWikiAsync(
        ISandboxFileReader reader, string wikiDir, CancellationToken cancellationToken)
    {
        var content = await reader.TryReadAsync(Path.Combine(wikiDir, IndexFile), cancellationToken);
        return content ?? string.Empty;
    }

    public static async Task<List<RunData>> ReadRunDataAsync(
        ISandboxFileReader reader, List<RunDirectoryInfo> runs, CancellationToken cancellationToken)
    {
        var result = new List<RunData>();
        foreach (var run in runs)
        {
            var plan = await reader.TryReadAsync(Path.Combine(run.Path, "plan.md"), cancellationToken)
                ?? string.Empty;
            var runResult = await reader.TryReadAsync(Path.Combine(run.Path, "result.md"), cancellationToken)
                ?? string.Empty;
            result.Add(new RunData(run.RunId, run.Name, plan, runResult));
        }
        return result;
    }

    public static Task WriteLastCompiledAsync(
        ISandboxFileReader reader, string wikiDir, string runId, CancellationToken cancellationToken)
        => reader.WriteAsync(Path.Combine(wikiDir, LastCompiledFile), runId, cancellationToken);

    private const int RunIdLength = 24;

    private static string? TryExtractRunId(string dirName)
    {
        // Canonical RunId is 24 chars (yyyy-MM-ddTHH-mm-ss-{4hex}); directory
        // adds "-{slug}". A bare RunId-only directory (no slug) is also legal;
        // pre-p0156 r{NN} names are explicitly NOT recognised.
        if (dirName.Length < RunIdLength) return null;
        var prefix = dirName[..RunIdLength];
        return RunIdGenerator.IsValid(prefix) ? prefix : null;
    }

    private static string LastSegment(string path)
    {
        var idx = path.LastIndexOf('/');
        return idx < 0 ? path : path[(idx + 1)..];
    }

    public sealed record RunDirectoryInfo(string Path, string RunId, string Name);
    public sealed record RunData(string RunId, string DirName, string Plan, string Result);
}
