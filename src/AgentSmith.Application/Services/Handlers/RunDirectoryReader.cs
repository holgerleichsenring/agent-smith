using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Handlers;

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
            if (name.Length >= 3 && name[0] == 'r' && int.TryParse(name[1..3], out var num))
                result.Add(new RunDirectoryInfo(dir, num, name));
        }
        return result.OrderBy(r => r.RunNumber).ToList();
    }

    public static async Task<int> ReadLastCompiledAsync(
        ISandboxFileReader reader, string wikiDir, CancellationToken cancellationToken)
    {
        var content = await reader.TryReadAsync(Path.Combine(wikiDir, LastCompiledFile), cancellationToken);
        if (content is null) return 0;
        return int.TryParse(content.Trim(), out var num) ? num : 0;
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
            result.Add(new RunData(run.RunNumber, run.Name, plan, runResult));
        }
        return result;
    }

    public static Task WriteLastCompiledAsync(
        ISandboxFileReader reader, string wikiDir, int runNumber, CancellationToken cancellationToken)
        => reader.WriteAsync(Path.Combine(wikiDir, LastCompiledFile), runNumber.ToString(), cancellationToken);

    private static string LastSegment(string path)
    {
        var idx = path.LastIndexOf('/');
        return idx < 0 ? path : path[(idx + 1)..];
    }

    public sealed record RunDirectoryInfo(string Path, int RunNumber, string Name);
    public sealed record RunData(int RunNumber, string DirName, string Plan, string Result);
}
