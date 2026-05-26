using System.Globalization;
using System.Text.RegularExpressions;
using AgentSmith.Server.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Server.Services;

/// <summary>
/// p0169a: parses a run's result.md YAML frontmatter into
/// <see cref="RunMetaFrontmatter"/>. Pre-p0169a runs lack the topology
/// fields — those default to "unknown" placeholders so the dashboard
/// renders them without crashing.
///
/// The runs root is configured via the AGENTSMITH_RUNS_ROOT env var.
/// Each immediate subdirectory whose name starts with a 4-digit year is
/// treated as a run; the runId is derived from the directory name's
/// canonical prefix (before the first "-<slug>" segment).
/// </summary>
public sealed class RunMetaReader(IRunsRootResolver rootResolver)
{
    private static readonly Regex FrontmatterRegex = new(
        @"^---\s*\n(?<body>[\s\S]*?)\n---\s*\n",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public IReadOnlyList<RunMetaFrontmatter> ListAll()
    {
        var root = rootResolver.Resolve();
        if (!Directory.Exists(root)) return [];

        var list = new List<RunMetaFrontmatter>();
        foreach (var dir in Directory.EnumerateDirectories(root))
        {
            var meta = TryReadFrom(dir);
            if (meta is not null) list.Add(meta);
        }
        // Newest first: directory names start with sortable UTC timestamp.
        list.Sort((a, b) => string.Compare(b.RunId, a.RunId, StringComparison.Ordinal));
        return list;
    }

    public RunMetaFrontmatter? Read(string runId)
    {
        var root = rootResolver.Resolve();
        if (!Directory.Exists(root)) return null;
        foreach (var dir in Directory.EnumerateDirectories(root))
        {
            var dirName = Path.GetFileName(dir);
            if (!dirName.StartsWith(runId, StringComparison.Ordinal)) continue;
            return TryReadFrom(dir);
        }
        return null;
    }

    public string? GetRunDir(string runId)
    {
        var root = rootResolver.Resolve();
        if (!Directory.Exists(root)) return null;
        foreach (var dir in Directory.EnumerateDirectories(root))
        {
            var dirName = Path.GetFileName(dir);
            if (dirName.StartsWith(runId, StringComparison.Ordinal)) return dir;
        }
        return null;
    }

    private RunMetaFrontmatter? TryReadFrom(string runDir)
    {
        var resultPath = Path.Combine(runDir, "result.md");
        if (!File.Exists(resultPath)) return null;
        var content = File.ReadAllText(resultPath);
        var match = FrontmatterRegex.Match(content);
        var dirName = Path.GetFileName(runDir);
        var runId = ExtractRunId(dirName);

        if (!match.Success)
            return new RunMetaFrontmatter(runId, null, "unknown", null, 0, "unknown", 0, [], null, null);

        Dictionary<string, object?> map;
        try
        {
            map = YamlDeserializer.Deserialize<Dictionary<string, object?>>(match.Groups["body"].Value)
                ?? new Dictionary<string, object?>();
        }
        catch (YamlDotNet.Core.YamlException)
        {
            return new RunMetaFrontmatter(runId, null, "unknown", null, 0, "unknown", 0, [], null, null);
        }

        var pipelineName = GetString(map, "pipeline_name");
        var status = GetString(map, "status") ?? GetString(map, "result");
        var startedAt = ParseDateTime(GetString(map, "started_at"));
        var duration = GetInt(map, "duration_seconds");
        var repoMode = GetString(map, "repo_mode");
        var sandboxCount = GetInt(map, "sandbox_count");
        var repos = GetStringList(map, "repos");
        var ticket = GetString(map, "ticket");
        var type = GetString(map, "type");

        return new RunMetaFrontmatter(
            runId,
            pipelineName,
            status ?? "unknown",
            startedAt,
            duration,
            repoMode ?? "unknown",
            sandboxCount,
            repos,
            ticket,
            type);
    }

    private static string ExtractRunId(string dirName)
    {
        // Run dirs are "{runId}-{slug}". RunId is yyyy-MM-ddTHH-mm-ss-XXXX
        // (5 hyphens, 6 segments after Split('-')); slug follows.
        var parts = dirName.Split('-');
        if (parts.Length < 6) return dirName;
        return string.Join('-', parts.Take(6));
    }

    private static string? GetString(IDictionary<string, object?> map, string key)
        => map.TryGetValue(key, out var v) ? v?.ToString() : null;

    private static int GetInt(IDictionary<string, object?> map, string key)
        => map.TryGetValue(key, out var v)
            && int.TryParse(v?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                ? n : 0;

    private static DateTimeOffset? ParseDateTime(string? s)
        => s is not null
           && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture,
               DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var t)
                ? t : null;

    private static IReadOnlyList<string> GetStringList(IDictionary<string, object?> map, string key)
    {
        if (!map.TryGetValue(key, out var v) || v is null) return [];
        if (v is List<object> list)
            return list.Select(x => x?.ToString() ?? string.Empty).Where(s => s.Length > 0).ToList();
        if (v is string s) return [s];
        return [];
    }
}

/// <summary>p0169a: configurable runs-root resolver. Defaults to the standard
/// <c>.agentsmith/runs/</c> under the deployment-configured runs root.</summary>
public interface IRunsRootResolver
{
    string Resolve();
}

public sealed class EnvRunsRootResolver : IRunsRootResolver
{
    public string Resolve() =>
        Environment.GetEnvironmentVariable("AGENTSMITH_RUNS_ROOT") ?? "/app/runs";
}
