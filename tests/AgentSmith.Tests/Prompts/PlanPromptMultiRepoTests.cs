using AgentSmith.Application.Prompts;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

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

    // p0394: the multi-repo rules render INSIDE the embedded template
    // ({MultiRepoRulesSection}), before its '## Respond in JSON format:'
    // section — appended after the template's JSON-only terminal instruction
    // (p0384) they made the model answer prose (0-step plan on a live run).
    // These three render through the real embedded catalog, not the
    // passthrough, because the property under test is position.
    [Fact]
    public void BuildPlanSystemPrompt_MultiRepo_RulesRenderBeforeJsonFormatSection()
    {
        var prompt = new AgentPromptBuilder(EmbeddedCatalog())
            .BuildPlanSystemPrompt("principles", TwoRepoCodeMaps());

        var rules = prompt.IndexOf("## Multi-repository plan rules", StringComparison.Ordinal);
        var jsonSection = prompt.IndexOf("## Respond in JSON format:", StringComparison.Ordinal);
        rules.Should().BeGreaterThan(-1);
        jsonSection.Should().BeGreaterThan(rules);
    }

    [Fact]
    public void BuildPlanSystemPrompt_MultiRepo_JsonOnlyInstructionStaysTerminal()
    {
        var prompt = new AgentPromptBuilder(EmbeddedCatalog())
            .BuildPlanSystemPrompt("principles", TwoRepoCodeMaps());

        prompt.TrimEnd().Should().EndWith("Respond ONLY with the JSON, no additional text.");
    }

    [Fact]
    public void BuildPlanSystemPrompt_SingleRepo_TokenRendersEmpty()
    {
        var prompt = new AgentPromptBuilder(EmbeddedCatalog())
            .BuildPlanSystemPrompt("principles",
                new Dictionary<string, string>(StringComparer.Ordinal) { ["server"] = "modules: [api]" });

        prompt.Should().NotContain("Multi-repository plan rules");
        prompt.Should().NotContain("{MultiRepoRulesSection}", "the token must be bound");
    }

    private static Dictionary<string, string> TwoRepoCodeMaps() =>
        new(StringComparer.Ordinal)
        {
            ["server"] = "modules: [api]",
            ["client"] = "modules: [ui]",
        };

    private static EmbeddedPromptCatalog EmbeddedCatalog() => new(
        new EnvDirectoryPromptOverrideSource(NullLogger<EnvDirectoryPromptOverrideSource>.Instance),
        NullLogger<EmbeddedPromptCatalog>.Instance);

    private static ProjectMap MapWith(string lang) => new(
        lang, [], [], [], [], new Conventions(null, null, null),
        new CiConfig(false, null, null, null));
}
