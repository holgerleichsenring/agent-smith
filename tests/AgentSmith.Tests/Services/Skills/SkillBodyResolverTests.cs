using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Skills;

/// <summary>
/// p0313b: the body resolver inlines a master's {{ref:&lt;slug&gt;}} citations from the
/// catalog's references/ directory, ONE level deep. Before it, the same spawn-budget
/// and evidence-mode prose lived in six master bodies and drifted between them,
/// because nothing compared the copies.
///
/// The failure modes are loud on purpose. A master that cites a reference the catalog
/// does not ship must not render — it would reach the model missing the rules it was
/// written to carry, and that is invisible in the output.
/// </summary>
public sealed class SkillBodyResolverTests
{
    private sealed class FakeReferences(Dictionary<string, string> bySlug) : ISkillReferenceSource
    {
        public List<string> Reads { get; } = [];

        public string? TryRead(string slug)
        {
            Reads.Add(slug);
            return bySlug.GetValueOrDefault(slug);
        }
    }

    private static FakeReferences References(params (string Slug, string Body)[] entries) =>
        new(entries.ToDictionary(e => e.Slug, e => e.Body));

    private static RoleSkillDefinition Skill(string name, string rules) =>
        new() { Name = name, Rules = rules };

    [Fact]
    public void Loader_MasterCitingAReference_InlinesItAtRender()
    {
        var resolver = new SkillBodyResolver(
            References(("spawn-budget", "The budget is finite — typically 20 children per run.")));

        var body = resolver.ResolveBody(
            Skill("security-master", "## Parallelism\n\n{{ref:spawn-budget}}\n"), SkillRole.Master);

        body.Should().Contain("typically 20 children per run");
        body.Should().NotContain("{{ref:", "the citation is replaced, not annotated");
        body.Should().StartWith("## Parallelism", "the master's own words stay where they were");
    }

    [Fact]
    public void Loader_MissingReference_FailsLoud()
    {
        var resolver = new SkillBodyResolver(References());

        var act = () => resolver.ResolveBody(
            Skill("security-master", "{{ref:spawn-budget}}"), SkillRole.Master);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*security-master*spawn-budget*references/spawn-budget.md*");
    }

    [Fact]
    public void Loader_NestedReference_FailsLoud()
    {
        var resolver = new SkillBodyResolver(References(
            ("phase-discipline", "Refute first.\n\n{{ref:evidence-modes}}"),
            ("evidence-modes", "potential | confirmed | analyzed_from_source")));

        var act = () => resolver.ResolveBody(
            Skill("security-master", "{{ref:phase-discipline}}"), SkillRole.Master);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*one level deep*");
    }

    [Fact]
    public void SharedRef_EditOnce_EveryCitingMasterRendersTheChange()
    {
        // The point of the phase: one file, one edit, every citing master changed.
        var references = References(("spawn-budget", "Budget: 20 children, run-wide."));
        var resolver = new SkillBodyResolver(references);

        var security = resolver.ResolveBody(
            Skill("security-master", "## Parallelism\n{{ref:spawn-budget}}"), SkillRole.Master);
        var coding = resolver.ResolveBody(
            Skill("coding-agent-master", "## SubAgent Guidance\n{{ref:spawn-budget}}"), SkillRole.Master);
        var legal = resolver.ResolveBody(
            Skill("legal-analyst-master", "## SubAgent Guidance\n{{ref:spawn-budget}}"), SkillRole.Master);

        new[] { security, coding, legal }.Should().OnlyContain(
            b => b.Contains("Budget: 20 children, run-wide."),
            "one reference file is the single source of the policy");
    }

    [Fact]
    public void ResolveBody_MultipleCitations_AllInlined()
    {
        var resolver = new SkillBodyResolver(References(
            ("phase-discipline", "Refute before you deliver."),
            ("evidence-modes", "potential | confirmed | analyzed_from_source"),
            ("spawn-budget", "Budget: 20 children.")));

        var body = resolver.ResolveBody(
            Skill("security-master",
                "{{ref:phase-discipline}}\n## Verify\n{{ref:evidence-modes}}\n## Parallelism\n{{ref:spawn-budget}}"),
            SkillRole.Master);

        body.Should().Contain("Refute before you deliver.");
        body.Should().Contain("analyzed_from_source");
        body.Should().Contain("Budget: 20 children.");
    }

    [Fact]
    public void ResolveBody_SlugThatEscapesTheReferencesDirectory_FailsBeforeAnyRead()
    {
        // The slug becomes a file path. It is validated before the source ever sees it,
        // so a catalog cannot be talked into reading outside references/.
        var references = References();
        var resolver = new SkillBodyResolver(references);

        var act = () => resolver.ResolveBody(
            Skill("security-master", "{{ref:../../etc/passwd}}"), SkillRole.Master);

        act.Should().Throw<InvalidOperationException>().WithMessage("*not a valid reference name*");
        references.Reads.Should().BeEmpty();
    }

    [Fact]
    public void ResolveBody_NoCitations_ReturnsRulesVerbatim()
    {
        // The overwhelmingly common case, and the one a catalog pinned before this
        // phase produces: no citation, no reference lookup, byte-identical body.
        var references = References();
        var resolver = new SkillBodyResolver(references);
        var skill = Skill("architect", "Plan the implementation.");

        var resolved = resolver.ResolveBody(skill, SkillRole.Lead);

        resolved.Should().Be(skill.Rules);
        references.Reads.Should().BeEmpty();
    }

    [Fact]
    public void ResolveBody_RepeatedCalls_CachesResult()
    {
        var resolver = new SkillBodyResolver(References());
        var skill = Skill("cache-test", "body");

        var first = resolver.ResolveBody(skill, SkillRole.Analyst);
        var second = resolver.ResolveBody(skill, SkillRole.Analyst);

        ReferenceEquals(first, second).Should().BeTrue("cached body string is interned per (skill, role) tuple");
    }

    [Fact]
    public void ResolveBody_DifferentRoles_CachedSeparately()
    {
        var resolver = new SkillBodyResolver(References());
        var skill = Skill("multi", "shared body");

        var lead = resolver.ResolveBody(skill, SkillRole.Lead);
        var analyst = resolver.ResolveBody(skill, SkillRole.Analyst);

        lead.Should().Be(analyst);
    }
}
