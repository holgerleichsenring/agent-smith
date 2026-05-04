using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services.Skills;

public sealed class SkillMdParserExtendedTests : IDisposable
{
    private readonly string _tempDir;
    private readonly YamlSkillLoader _loader;

    public SkillMdParserExtendedTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "agentsmith-skill-ext-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        _loader = new YamlSkillLoader(
            new StubSkillsCatalogPath(),
            new ConceptVocabularyLoader(NullLogger<ConceptVocabularyLoader>.Instance),
            new ConceptVocabularyValidator(NullLogger<ConceptVocabularyValidator>.Instance),
            new SkillIndexBuilder(NullLogger<SkillIndexBuilder>.Instance),
            new ProviderOverrideResolver(new ActiveProviderResolver(new AgentSmithConfig())),
            NullLogger<YamlSkillLoader>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void RolesSupportedFrontmatter_ParsesAllRoles()
    {
        WriteSkill("architect", """
            ---
            name: architect
            version: 2.0.0
            description: "test"

            roles_supported: [lead, analyst, reviewer]

            activation:
              positive:
                - {key: pattern_decision, desc: "Pattern decision"}
              negative:
                - {key: pure_bugfix, desc: "Pure bug fix"}

            role_assignment:
              lead:
                positive:
                  - {key: pattern_primary, desc: "Pattern primary"}
              analyst:
                positive:
                  - {key: secondary, desc: "Secondary"}
              reviewer:
                positive:
                  - {key: layer_touch, desc: "Layer touch"}

            output_contract:
              schema_ref: skill-observation
              hard_limits:
                max_observations: 8
                max_chars_per_field: 200
              output_type:
                lead: plan
                analyst: list
                reviewer: list
            ---

            ## as_lead
            Lead body.

            ## as_analyst
            Analyst body.

            ## as_reviewer
            Reviewer body.
            """);

        var roles = _loader.LoadRoleDefinitions(Path.Combine(_tempDir, "skills"));
        var skill = roles.Should().ContainSingle().Which;

        skill.RolesSupported.Should().BeEquivalentTo(new[]
        {
            SkillRole.Lead, SkillRole.Analyst, SkillRole.Reviewer
        });
        skill.Activation.Should().NotBeNull();
        skill.Activation!.Positive.Should().ContainSingle(k => k.Key == "pattern_decision");
        skill.Activation.Negative.Should().ContainSingle(k => k.Key == "pure_bugfix");
        skill.RoleAssignments.Should().HaveCount(3);
        skill.OutputContract.Should().NotBeNull();
        skill.OutputContract!.OutputType[SkillRole.Lead].Should().Be(OutputForm.Plan);
        skill.OutputContract.OutputType[SkillRole.Analyst].Should().Be(OutputForm.List);
        skill.OutputContract.OutputType[SkillRole.Reviewer].Should().Be(OutputForm.List);
    }

    [Fact]
    public void BodySplitByH2Header_ProducesRoleBodyMap()
    {
        WriteSkill("multi", """
            ---
            name: multi
            version: 2.0.0
            description: "test"

            roles_supported: [analyst, reviewer]
            ---

            ## as_analyst

            Analyst section text here.

            ## as_reviewer

            Reviewer section text here.
            """);

        var skill = _loader.LoadRoleDefinitions(Path.Combine(_tempDir, "skills")).Single();

        skill.RoleBodies.Should().NotBeNull();
        skill.RoleBodies![SkillRole.Analyst].Should().Contain("Analyst section text");
        skill.RoleBodies[SkillRole.Reviewer].Should().Contain("Reviewer section text");
    }

    [Fact]
    public void DeclaresRole_ButNoBodySection_Rejected()
    {
        WriteSkill("incomplete", """
            ---
            name: incomplete
            version: 2.0.0
            description: "test"

            roles_supported: [analyst, reviewer]
            ---

            ## as_analyst

            Only one section — reviewer is declared but missing.
            """);

        var roles = _loader.LoadRoleDefinitions(Path.Combine(_tempDir, "skills"));
        roles.Should().BeEmpty();
    }

    [Fact]
    public void RoleAssignment_DeclaresRoleNotInRolesSupported_Rejected()
    {
        WriteSkill("inconsistent", """
            ---
            name: inconsistent
            version: 2.0.0
            description: "test"

            roles_supported: [analyst]

            role_assignment:
              reviewer:
                positive:
                  - {key: foo, desc: "Foo"}
            ---

            ## as_analyst

            Body.
            """);

        var roles = _loader.LoadRoleDefinitions(Path.Combine(_tempDir, "skills"));
        roles.Should().BeEmpty();
    }

    [Fact]
    public void DuplicateReferenceId_Rejected()
    {
        WriteSkill("dup-refs", """
            ---
            name: dup-refs
            version: 2.0.0
            description: "test"

            roles_supported: [analyst]

            references:
              - {id: shared, path: a.md}
              - {id: shared, path: b.md}
            ---

            ## as_analyst

            Body.
            """);

        var roles = _loader.LoadRoleDefinitions(Path.Combine(_tempDir, "skills"));
        roles.Should().BeEmpty();
    }

    private void WriteSkill(string name, string content)
    {
        var dir = Path.Combine(_tempDir, "skills", name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SKILL.md"), content);
    }
}
