using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.SpecExecution;

/// <summary>
/// 2026-11-26-462aa: moves every context's state.done out of its context.yaml into its done.md — the entries in their
/// order below the lines done.md already holds, the block spliced out. Run by the record step's write, so a repository
/// is migrated by its first record and nothing reads two layouts. A context.yaml that cannot be edited safely is left
/// as it is and named in the log.
/// </summary>
public sealed class ContextDoneMigration(IContextYamlStateDoneCodec codec, ILogger<ContextDoneMigration> logger)
{
    /// <summary>What to write: done.md text and context.yaml text per migrated context name.</summary>
    public sealed record Plan(IReadOnlyDictionary<string, string> Done, IReadOnlyDictionary<string, string> Yaml);

    public async Task<Plan> PlanAsync(ISandboxFileReader reader, string repoLocalPath, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var done = new Dictionary<string, string>(StringComparer.Ordinal);
        var yamls = new Dictionary<string, string>(StringComparer.Ordinal);
        var root = $"{repoLocalPath.TrimEnd('/')}/{ProjectMetaPaths.Contexts}";
        foreach (var name in ContextNames(await reader.ListAsync(root, maxDepth: 1, ct), root))
        {
            if (await reader.TryReadAsync(Path.Combine(repoLocalPath, YamlPath(name)), ct) is not { } yaml) continue;
            var extracted = codec.Extract(yaml);
            if (extracted.Error is { } error)
            {
                logger.LogWarning("{Path} keeps its state.done — {Reason}", YamlPath(name), error);
                continue;
            }
            if (extracted.Yaml is null) continue;
            done[name] = ContextDoneIndex.Append(await reader.TryReadAsync(Path.Combine(repoLocalPath, DonePath(name)), ct),
                extracted.Entries);
            yamls[name] = extracted.Yaml;
            logger.LogInformation("{Path}: {Count} state.done entries moved to {Done}", YamlPath(name), extracted.Entries.Count, DonePath(name));
        }
        return new Plan(done, yamls);
    }

    public static string DonePath(string context) =>
        $"{ProjectMetaPaths.Contexts}/{context}/{ProjectMetaPaths.ContextDoneFile}";

    public static string YamlPath(string context) =>
        $"{ProjectMetaPaths.Contexts}/{context}/{ProjectMetaPaths.ContextYamlFile}";

    private static IEnumerable<string> ContextNames(IReadOnlyList<string> entries, string root) =>
        entries.Where(e => e.StartsWith(root + "/", StringComparison.Ordinal))
            .Select(e => e[(root.Length + 1)..].TrimEnd('/'))
            .Where(rel => rel.Length > 0 && !rel.Contains('/'))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);
}
