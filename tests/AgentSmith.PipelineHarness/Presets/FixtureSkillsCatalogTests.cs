using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199d: pins the checked-in fixture catalog against the production
/// YamlSkillLoader's contract. The harness uses these role files to populate
/// AvailableRoles; if an upstream loader change drops them silently, the
/// init-project / autonomous tests would surface it as a confusing "0 roles
/// loaded" failure further down the chain. This test stays close to the
/// fixture so the failure mode reads as "fixture/loader contract drift".
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class FixtureSkillsCatalogTests
{
    [Fact]
    public async Task FixtureSkillsCatalog_AllSkillsParseUnderYamlSkillLoader_NoValidationErrors()
    {
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default),
            SandboxBackend.Stub, session: null, SkillsBackend.Fixture);
        var catalog = (CheckedInSkillsCatalogPath)
            harness.Services.GetService(typeof(AgentSmith.Contracts.Services.ISkillsCatalogPath))!;
        var loader = (AgentSmith.Contracts.Services.ISkillLoader)
            harness.Services.GetService(typeof(AgentSmith.Contracts.Services.ISkillLoader))!;

        var roles = loader.LoadRoleDefinitions(Path.Combine(catalog.Root, "skills", "coding"));

        roles.Should().HaveCount(6,
            "csharp/node/python/generic bootstrap + autonomous-planner + autonomous-investigator");
        roles.Should().Contain(r => r.Name == "csharp-bootstrap" && r.OutputSchema == "bootstrap");
        roles.Should().Contain(r => r.Name == "autonomous-planner" && r.OutputSchema == "plan");
        roles.Should().Contain(r => r.Name == "autonomous-investigator"
            && r.Role == "investigator");
    }
}
