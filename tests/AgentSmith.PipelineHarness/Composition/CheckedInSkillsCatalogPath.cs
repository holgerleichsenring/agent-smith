using AgentSmith.Contracts.Services;

namespace AgentSmith.PipelineHarness.Composition;

/// <summary>
/// p0199d: ISkillsCatalogPath pointed at the checked-in fixture catalog
/// under Fixtures/SkillsCatalog/. Selected by SkillsBackend.Fixture so
/// InitProject + Autonomous tests get a populated AvailableRoles without
/// touching the network or the real agent-smith-skills tree. The fixture
/// root carries a <c>skills/</c> subdirectory matching the production
/// catalog layout (so LoadSkillsHandler's catalog-relative resolve hits
/// <c>&lt;root&gt;/skills/coding</c> for the default skills_path).
/// </summary>
internal sealed class CheckedInSkillsCatalogPath : ISkillsCatalogPath
{
    public CheckedInSkillsCatalogPath()
    {
        Root = Path.Combine(AppContext.BaseDirectory, "Fixtures", "SkillsCatalog");
    }

    public string Root { get; }
}
