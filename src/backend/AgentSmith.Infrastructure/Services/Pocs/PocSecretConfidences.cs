using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Services.Security;

namespace AgentSmith.Infrastructure.Services.Pocs;

/// <summary>2026-11-23-933dd: the confidence of each 'secrets' pattern of the skills catalog, loaded as the code stage's
/// <see cref="PocPatternSet"/> loads them; none loaded is a failure naming the directory.</summary>
public sealed class PocSecretConfidences(PatternsDirectoryResolver directories, PatternDefinitionLoader loader) : IPocSecretConfidences
{
    public (IReadOnlyDictionary<string, int> ByPattern, string? Failure) Read()
    {
        var directory = directories.Resolve();
        var secrets = loader.LoadFromDirectory(directory).Where(PocPatternSet.IsSecret).ToList();
        if (secrets.Count == 0)
            return (new Dictionary<string, int>(), $"no secret patterns loaded from {directory}; the image scan cannot tell which secrets block");
        return (secrets.GroupBy(s => s.Id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Max(s => s.Confidence), StringComparer.Ordinal), null);
    }
}
