using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Prompts;

// p0384: the plan prompt enumerates EVERY scoped repo — one analysis block per
// repo, repo-prefixed step targets, and an explicit coverage rule — instead of
// the arbitrary first-configured repo (ticket #19106 root cause). Shape tests:
// they assert repo enumeration, not exact wording.
public sealed class PlanPromptMultiRepoTests
{
    private sealed class PassthroughCatalog : IPromptCatalog
    {
        public string Get(string name) => name;
        public string Render(string name, IReadOnlyDictionary<string, string> tokens) =>
            string.Join("\n", tokens.Values.Where(v => !string.IsNullOrEmpty(v)));
    }

    private static readonly Ticket Ticket = new(
        new TicketId("42"), "Add tracing", "Trace across services", null, "Open", "AzureDevOps");

    [Fact]
    public void BuildPlanUserPrompt_ThreeRepos_RendersOneAnalysisBlockPerRepo()
    {
        var maps = new Dictionary<string, ProjectMap>(StringComparer.Ordinal)
        {
            ["server"] = MapWith("csharp"),
            ["client"] = MapWith("typescript"),
            ["docs"] = MapWith("markdown"),
        };

        var prompt = new AgentPromptBuilder(new PassthroughCatalog())
            .BuildPlanUserPrompt(Ticket, maps);

        prompt.Should().Contain("## Repository: server");
        prompt.Should().Contain("## Repository: client");
        prompt.Should().Contain("## Repository: docs");
        prompt.Should().Contain("csharp").And.Contain("typescript").And.Contain("markdown");
    }

    [Fact]
    public void BuildPlanUserPrompt_SingleRepo_RendersThatRepo()
    {
        var maps = new Dictionary<string, ProjectMap>(StringComparer.Ordinal)
        {
            ["server"] = MapWith("csharp"),
        };

        var prompt = new AgentPromptBuilder(new PassthroughCatalog())
            .BuildPlanUserPrompt(Ticket, maps);

        prompt.Should().Contain("## Repository: server");
        prompt.Should().Contain("csharp");
    }

    [Fact]
    public void BuildPlanSystemPrompt_MultiRepo_InstructsRepoPrefixedTargetsAndCoverage()
    {
        var codeMaps = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["server"] = "modules: [api]",
            ["client"] = "modules: [ui]",
        };

        var prompt = new AgentPromptBuilder(new PassthroughCatalog())
            .BuildPlanSystemPrompt("principles", codeMaps);

        prompt.Should().Contain("server").And.Contain("client");
        prompt.Should().Contain("Multi-repository plan rules");
        prompt.Should().Contain("prefixed with the repository");
        prompt.Should().Contain("not affected");
    }

    [Fact]
    public void BuildPlanSystemPrompt_SingleRepo_EmitsNoMultiRepoRules()
    {
        var codeMaps = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["server"] = "modules: [api]",
        };

        var prompt = new AgentPromptBuilder(new PassthroughCatalog())
            .BuildPlanSystemPrompt("principles", codeMaps);

        prompt.Should().NotContain("Multi-repository plan rules");
    }

    private static ProjectMap MapWith(string lang) => new(
        lang, [], [], [], [], new Conventions(null, null, null),
        new CiConfig(false, null, null, null));
}
