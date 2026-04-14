namespace AgentSmith.Application.Services.Handlers;

internal static class RunDirectoryReader
{
    private const string LastCompiledFile = ".last-compiled";
    private const string IndexFile = "index.md";

    public static List<RunDirectoryInfo> GetRunDirectories(string runsDir)
    {
        var result = new List<RunDirectoryInfo>();
        foreach (var dir in Directory.GetDirectories(runsDir))
        {
            var name = Path.GetFileName(dir);
            if (name.Length >= 3 && name[0] == 'r' && int.TryParse(name[1..3], out var num))
            {
                result.Add(new RunDirectoryInfo(dir, num, name));
            }
        }

        return result.OrderBy(r => r.RunNumber).ToList();
    }

    public static int ReadLastCompiled(string wikiDir)
    {
        var path = Path.Combine(wikiDir, LastCompiledFile);
        if (!File.Exists(path))
            return 0;

        var content = File.ReadAllText(path).Trim();
        return int.TryParse(content, out var num) ? num : 0;
    }

    public static string ReadExistingWiki(string wikiDir)
    {
        var indexPath = Path.Combine(wikiDir, IndexFile);
        if (!File.Exists(indexPath))
            return string.Empty;

        return File.ReadAllText(indexPath);
    }

    public static async Task<List<RunData>> ReadRunDataAsync(
        List<RunDirectoryInfo> runs, CancellationToken ct)
    {
        var result = new List<RunData>();
        foreach (var run in runs)
        {
            var planPath = Path.Combine(run.Path, "plan.md");
            var resultPath = Path.Combine(run.Path, "result.md");

            var plan = File.Exists(planPath)
                ? await File.ReadAllTextAsync(planPath, ct)
                : string.Empty;

            var runResult = File.Exists(resultPath)
                ? await File.ReadAllTextAsync(resultPath, ct)
                : string.Empty;

            result.Add(new RunData(run.RunNumber, run.Name, plan, runResult));
        }

        return result;
    }

    public static async Task WriteLastCompiledAsync(
        string wikiDir, int runNumber, CancellationToken ct)
    {
        var path = Path.Combine(wikiDir, LastCompiledFile);
        await File.WriteAllTextAsync(path, runNumber.ToString(), ct);
    }

    internal sealed record RunDirectoryInfo(string Path, int RunNumber, string Name);
    internal sealed record RunData(int RunNumber, string DirName, string Plan, string Result);
}
