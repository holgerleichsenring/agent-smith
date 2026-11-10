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
public sealed class RunDirectoryReader
{
    private const string LastCompiledFile = ".last-compiled";

    public async Task<List<RunDirectoryInfo>> GetRunDirectoriesAsync(
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

    public async Task<string> ReadLastCompiledAsync(
        ISandboxFileReader reader, string wikiDir, CancellationToken cancellationToken)
    {
        var content = await reader.TryReadAsync(Path.Combine(wikiDir, LastCompiledFile), cancellationToken);
        return content?.Trim() ?? string.Empty;
    }

    // 2026-11-09-0620h/k: every page, not only the index, each between its own markers. A page the compile returns
    // replaces the stored one, so a model shown only index.md rewrote the other pages from nothing. Pages are shown
    // index first, then by name, within a character budget; the ones that do not fit are named as left out, and the
    // skill does not let the compile return a page it was not shown.
    internal const int WikiBudgetChars = 60_000;

    public async Task<string> ReadExistingWikiAsync(
        ISandboxFileReader reader, string wikiDir, CancellationToken cancellationToken)
    {
        var entries = await reader.ListAsync(wikiDir, maxDepth: 1, cancellationToken);
        var pages = entries.Where(e => e.EndsWith(".md", StringComparison.Ordinal))
            .OrderBy(e => LastSegment(e) == "index.md" ? 0 : 1).ThenBy(e => e, StringComparer.Ordinal);
        var sb = new System.Text.StringBuilder();
        var left = new List<string>();
        foreach (var path in pages)
        {
            var content = await reader.TryReadAsync(path, cancellationToken);
            if (content is null) continue;
            var page = $"<page name=\"{LastSegment(path)}\">\n{content.TrimEnd()}\n</page>\n";
            if (sb.Length + page.Length > WikiBudgetChars) { left.Add(LastSegment(path)); continue; }
            sb.Append(page);
        }
        if (left.Count > 0) sb.Append("Left out to fit: ").AppendJoin(", ", left).AppendLine(".");
        return sb.ToString().TrimEnd();
    }

    public async Task<List<RunData>> ReadRunDataAsync(
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

    public Task WriteLastCompiledAsync(
        ISandboxFileReader reader, string wikiDir, string runId, CancellationToken cancellationToken)
        => reader.WriteAsync(Path.Combine(wikiDir, LastCompiledFile), runId, cancellationToken);

    private const int RunIdLength = 24;

    private string? TryExtractRunId(string dirName)
    {
        // Canonical RunId is 24 chars (yyyy-MM-ddTHH-mm-ss-{4hex}); directory
        // adds "-{slug}". A bare RunId-only directory (no slug) is also legal;
        // pre-p0156 r{NN} names are explicitly NOT recognised.
        if (dirName.Length < RunIdLength) return null;
        var prefix = dirName[..RunIdLength];
        return RunIdGenerator.IsValid(prefix) ? prefix : null;
    }

    private string LastSegment(string path)
    {
        var idx = path.LastIndexOf('/');
        return idx < 0 ? path : path[(idx + 1)..];
    }

    public sealed record RunDirectoryInfo(string Path, string RunId, string Name);
    public sealed record RunData(string RunId, string DirName, string Plan, string Result);
}
