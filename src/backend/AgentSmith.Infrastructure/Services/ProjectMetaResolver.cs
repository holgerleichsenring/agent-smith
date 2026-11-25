using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Services;

/// <summary>
/// Lists .agentsmith/contexts/ immediate sub-directories via ISandboxFileReader,
/// reads each context.yaml via IContextYamlParser, and projects one MetaDiscovery
/// per discovered context. Empty list for repos missing .agentsmith/contexts/
/// (un-init / pre-v2 layouts) — coordinator's synthetic-default discovery covers
/// those upstream (p0161).
/// </summary>
public sealed class ProjectMetaResolver(IContextYamlParser contextYamlParser) : IProjectMetaResolver
{
    public async Task<IReadOnlyList<MetaDiscovery>> ResolveAllAsync(
        ISandboxFileReader reader, string root, CancellationToken cancellationToken)
    {
        var contextsRoot = $"{root.TrimEnd('/')}/{ProjectMetaPaths.Contexts}";
        var entries = await reader.ListAsync(contextsRoot, maxDepth: 1, cancellationToken);
        if (entries.Count == 0) return [];

        var discoveries = new List<MetaDiscovery>();
        foreach (var subDir in EnumerateImmediateSubDirs(entries, contextsRoot))
        {
            var contextName = LastSegment(subDir);
            var yamlPath = subDir + "/context.yaml";
            var yaml = await reader.TryReadAsync(yamlPath, cancellationToken);
            if (yaml is null) continue;
            ContextYamlParseResult result;
            try
            {
                result = contextYamlParser.Parse(yaml);
            }
            catch (InvalidOperationException)
            {
                // Missing meta.workdir — log surfaces upstream in
                // SandboxLanguageResolver; here we just skip.
                continue;
            }
            if (result.Summary is null) continue;
            discoveries.Add(new MetaDiscovery(subDir, contextName, result.Summary.Workdir));
        }
        return discoveries;
    }

    private static IEnumerable<string> EnumerateImmediateSubDirs(IReadOnlyList<string> entries, string contextsRoot)
    {
        foreach (var entry in entries)
        {
            if (!entry.StartsWith(contextsRoot + "/", StringComparison.Ordinal)) continue;
            var rel = entry[(contextsRoot.Length + 1)..];
            if (rel.Contains('/')) continue;
            yield return entry;
        }
    }

    private static string LastSegment(string path)
    {
        var idx = path.LastIndexOf('/');
        return idx < 0 ? path : path[(idx + 1)..];
    }
}
