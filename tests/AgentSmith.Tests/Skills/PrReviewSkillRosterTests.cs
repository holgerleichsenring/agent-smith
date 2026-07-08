using AgentSmith.Application.Services.Activation;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Skills;

/// <summary>
/// p0167b: the four pr-review skills (agent-smith-skills, skills/pr-review/).
/// Parse tests load the real SKILL.md files through the production
/// YamlSkillLoader when the skills catalog is reachable (AGENTSMITH_TEST_SKILLS_DIR /
/// ./test-skills / adjacent checkout — same resolution as CodeAwareSkillContentTests)
/// and skip silently otherwise; the activation-filter/triage test always runs
/// against inline definitions that mirror the shipped frontmatter.
/// </summary>
public sealed class PrReviewSkillRosterTests
{
    private const string PrReviewActivation = "pipeline_name = \"pr-review\"";

    private static readonly string[] RosterNames =
    [
        "correctness-reviewer", "security-overlap-reviewer",
        "style-reviewer", "test-coverage-reviewer",
    ];

    [Fact]
    public void StyleReviewer_SkillMdParses_AndActivatesOnReviewPhase()
    {
        var skill = LoadRosterSkill("style-reviewer");
        if (skill is null) return; // catalog without pr-review roster — not available here

        skill.Role.Should().Be("judge", "judges are the Review-phase reviewer slot");
        skill.Category.Should().Be("style");
        skill.OutputSchema.Should().Be("observation");
        skill.ActivatesWhen.Should().Contain("pr-review");
        skill.Rules.Should().Contain("line_range");
    }

    [Fact]
    public void CorrectnessReviewer_SkillMdParses_AndActivatesOnReviewPhase()
    {
        var skill = LoadRosterSkill("correctness-reviewer");
        if (skill is null) return;

        skill.Role.Should().Be("judge");
        skill.Category.Should().Be("correctness");
        skill.OutputSchema.Should().Be("observation");
        skill.ActivatesWhen.Should().Contain("pr-review");
        skill.Rules.Should().Contain("line_range").And.Contain("high");
    }

    [Fact]
    public void TestCoverageReviewer_SkillMdParses_AndReadsProjectMapForTestProjects()
    {
        var skill = LoadRosterSkill("test-coverage-reviewer");
        if (skill is null) return;

        skill.Role.Should().Be("judge");
        skill.Category.Should().Be("test-coverage");
        skill.Rules.Should().Contain("Existing Tests",
            "the skill grounds itself in the ProjectMap-derived test-project block");
        skill.Rules.Should().Contain("ProjectMap");
        // Severity heuristic: public API untested = high, private helper = low.
        skill.Rules.Should().ContainEquivalentOf("public").And.ContainEquivalentOf("private");
    }

    [Fact]
    public void SecurityOverlapReviewer_SkillMdParses_AndReferencesSecurityScanCatalog()
    {
        var skill = LoadRosterSkill("security-overlap-reviewer");
        if (skill is null) return;

        skill.Role.Should().Be("judge");
        skill.Rules.Should().Contain("skills/security",
            "the skill wraps the existing catalog instead of duplicating it");
        skill.Rules.Should().Contain("auth-reviewer").And.Contain("secrets-detector")
            .And.Contain("injection-checker").And.Contain("supply-chain-auditor");
        skill.Rules.Should().Contain("security-scan");
    }

    [Fact]
    public void PrReviewRoster_ActivationFilter_OnlyReviewPhaseSkillsPicked()
    {
        var roster = LoadRoster() ?? InlineRoster();
        var withForeignSkills = roster.Concat(
        [
            Judge("architect-judge-foreign", "pipeline_name = \"fix-bug\""),
            new RoleSkillDefinition
            {
                Name = "auth-reviewer-foreign", Description = "d", Role = "investigator",
                InvestigatorMode = "verify_hint",
                ActivatesWhen = "pipeline_name = \"security-scan\"",
            },
        ]).ToList();
        var pipeline = new PipelineContext();
        var concepts = RunStateConceptsTestFactory
            .WithVocabulary(RunStateConceptsTestFactory.FallbackMinimal)(pipeline);
        concepts.SetEnum("pipeline_name", "pr-review");

        var filtered = NewFilter().Filter(withForeignSkills, concepts);
        var triage = NewSelector().Select(filtered);

        filtered.Select(s => s.Name).Should().BeEquivalentTo(RosterNames,
            "only the pr-review roster survives the activates_when filter");
        triage.Phases[PipelinePhase.Review].Reviewers.Should().BeEquivalentTo(RosterNames,
            "judges land in the Review phase dispatched by RunReviewPhase");
        triage.Phases[PipelinePhase.Plan].Analysts.Should().BeEmpty();
        triage.Phases[PipelinePhase.Plan].Lead.Should().BeNull();
        triage.Phases[PipelinePhase.Final].Filter.Should().BeNull();
    }

    [Fact]
    public void PrReviewRoster_ParsedFrontmatter_MatchesInlineExpectations()
    {
        var roster = LoadRoster();
        if (roster is null) return;

        roster.Select(s => s.Name).Should().BeEquivalentTo(RosterNames);
        roster.Should().AllSatisfy(s =>
        {
            s.Role.Should().Be("judge");
            s.OutputSchema.Should().Be("observation");
            s.ActivatesWhen.Should().Be(PrReviewActivation);
            s.Description.Length.Should().BeLessThanOrEqualTo(180,
                "the loader hard-drops skills whose description exceeds 200 chars — 180 is the guard margin");
        });
    }

    private static IReadOnlyList<RoleSkillDefinition> InlineRoster() =>
        RosterNames.Select(name => Judge(name, PrReviewActivation)).ToList();

    private static RoleSkillDefinition Judge(string name, string activatesWhen) => new()
    {
        Name = name,
        Description = name,
        Role = "judge",
        OutputSchema = "observation",
        ActivatesWhen = activatesWhen,
    };

    private static RoleSkillDefinition? LoadRosterSkill(string name) =>
        LoadRoster()?.FirstOrDefault(s => s.Name == name);

    private static IReadOnlyList<RoleSkillDefinition>? LoadRoster()
    {
        var skillsRoot = TestSkillsRoot.Resolve();
        if (skillsRoot is null) return null;
        var prReviewDir = Path.Combine(skillsRoot, "pr-review");
        if (!Directory.Exists(prReviewDir)) return null;
        return NewLoader().LoadRoleDefinitions(prReviewDir);
    }

    private static YamlSkillLoader NewLoader() => new(
        new StubSkillsCatalogPath(),
        new ConceptVocabularyLoader(
            new NoOpEventPublisher(), new AsyncLocalRunContextAccessor(),
            new NoOpSystemEventPublisher(), NullLogger<ConceptVocabularyLoader>.Instance),
        new ConceptVocabularyValidator(NullLogger<ConceptVocabularyValidator>.Instance),
        new SkillIndexBuilder(NullLogger<SkillIndexBuilder>.Instance),
        new ProviderOverrideResolver(new ActiveProviderResolver(new AgentSmithConfig())),
        new NoOpEventPublisher(),
        new AsyncLocalRunContextAccessor(),
        new NoOpSystemEventPublisher(),
        NullLogger<YamlSkillLoader>.Instance);

    private static ActivationSkillFilter NewFilter()
    {
        var parser = new ActivationExpressionParser(new ActivationExpressionTokenizer());
        return new ActivationSkillFilter(
            parser, new ActivationEvaluator(), NullLogger<ActivationSkillFilter>.Instance);
    }

    private static DeterministicTriageSelector NewSelector()
    {
        var parser = new ActivationExpressionParser(new ActivationExpressionTokenizer());
        return new DeterministicTriageSelector(new ActivationSpecificityScorer(
            parser, NullLogger<ActivationSpecificityScorer>.Instance));
    }
}
