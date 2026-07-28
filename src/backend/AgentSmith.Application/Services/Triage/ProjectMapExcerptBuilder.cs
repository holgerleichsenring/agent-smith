using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Triage;

/// <summary>
/// Projects the analyzed ProjectMaps onto the narrower TriageInput.ProjectMapExcerpt.
/// Concept matching against the ConceptVocabulary happens here so the LLM sees a
/// prefiltered concept list — substring match against framework names, primary
/// language, and module paths. Vocabulary read from PipelineContext; absent → empty.
/// p0384: reads the per-repo dictionary (the only analysis surface) and merges it
/// into the single excerpt shape triage consumes — frameworks and languages union
/// across repos; a dictionary of one reduces to the old single-repo excerpt.
/// </summary>
public sealed class ProjectMapExcerptBuilder
{
    public ProjectMapExcerpt Build(PipelineContext pipeline)
    {
        var map = ResolveProjectMap(pipeline);
        var vocabulary = ResolveVocabulary(pipeline);
        return new ProjectMapExcerpt(
            Stack: map.Frameworks,
            Type: map.PrimaryLanguage,
            Concepts: MatchConcepts(map, vocabulary),
            TestCapability: BuildTestCapability(map),
            CiCapability: BuildCiCapability(map));
    }

    private static ProjectMap ResolveProjectMap(PipelineContext pipeline)
    {
        if (!pipeline.TryGet<IReadOnlyDictionary<string, ProjectMap>>(
                ContextKeys.RepoProjectMaps, out var maps) || maps is null || maps.Count == 0)
            return EmptyProjectMap();
        return maps.Count == 1 ? maps.Values.First() : MergeMaps(maps.Values);
    }

    private static ProjectMap MergeMaps(IEnumerable<ProjectMap> maps)
    {
        var list = maps.ToList();
        var first = list[0];
        return first with
        {
            PrimaryLanguage = string.Join(",", list
                .Select(m => m.PrimaryLanguage)
                .Where(l => !string.IsNullOrEmpty(l))
                .Distinct(StringComparer.OrdinalIgnoreCase)),
            Frameworks = list.SelectMany(m => m.Frameworks)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Modules = list.SelectMany(m => m.Modules).ToList(),
            TestProjects = list.SelectMany(m => m.TestProjects).ToList(),
            EntryPoints = list.SelectMany(m => m.EntryPoints).Distinct().ToList(),
        };
    }

    private static ConceptVocabulary ResolveVocabulary(PipelineContext pipeline) =>
        pipeline.TryGet<ConceptVocabulary>(ContextKeys.ConceptVocabulary, out var loaded) && loaded is not null
            ? loaded
            : ConceptVocabulary.Empty;

    private static IReadOnlyList<string> MatchConcepts(ProjectMap map, ConceptVocabulary vocabulary)
    {
        if (vocabulary.Concepts.Count == 0) return Array.Empty<string>();
        var haystack = BuildSignalText(map);
        var matches = new List<string>();
        foreach (var (key, _) in vocabulary.Concepts)
        {
            if (haystack.Contains(key, StringComparison.OrdinalIgnoreCase))
                matches.Add(key);
        }
        return matches;
    }

    private static string BuildSignalText(ProjectMap map)
    {
        var parts = new List<string> { map.PrimaryLanguage };
        parts.AddRange(map.Frameworks);
        parts.AddRange(map.Modules.Select(m => m.Path));
        if (map.TestProjects.Count > 0) parts.Add("test");
        if (map.Ci.HasCi) parts.Add("ci");
        return string.Join(" ", parts).ToLowerInvariant();
    }

    private static TestCapability BuildTestCapability(ProjectMap map)
    {
        var hasSetup = map.TestProjects.Count > 0;
        var command = map.Ci.TestCommand;
        var runnable = hasSetup && !string.IsNullOrEmpty(command);
        return new TestCapability(hasSetup, command, runnable);
    }

    private static CiCapability BuildCiCapability(ProjectMap map) =>
        new(map.Ci.HasCi, DeploymentTarget: null);

    private static ProjectMap EmptyProjectMap() => new(
        PrimaryLanguage: "unknown",
        Frameworks: Array.Empty<string>(),
        Modules: Array.Empty<Module>(),
        TestProjects: Array.Empty<TestProject>(),
        EntryPoints: Array.Empty<string>(),
        Conventions: new Conventions(null, null, null),
        Ci: new CiConfig(false, null, null, null));
}
