using AgentSmith.Contracts.Services;

namespace AgentSmith.PipelineHarness.Composition;

/// <summary>
/// p0199: harness-owned <see cref="ISkillsCatalogPath"/>. Points at an
/// empty temp directory so handlers that read <c>Root</c> (e.g.
/// <c>ExecutePipelineUseCase.LoadVocabularyFromCatalog</c>,
/// <c>YamlSkillLoader</c>) see a path that exists rather than throwing
/// the "not resolved yet" guard. We never resolve a real catalog in the
/// harness — skill content is not what these tests assert on.
/// </summary>
internal sealed class FixtureSkillsCatalogPath : ISkillsCatalogPath
{
    public FixtureSkillsCatalogPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "agentsmith-harness-skills-empty");
        Directory.CreateDirectory(dir);
        Root = dir;
    }

    public string Root { get; }
}
