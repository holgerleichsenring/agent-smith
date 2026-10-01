using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.DesignSources;

/// <summary>2026-10-01-283dg: the design system section of the master body.</summary>
public sealed class DesignSystemPromptSectionTests
{
    private const string Frontmatter = "---\ncolors:\n  primary: \"#22c55e\"\n  ink: \"#201515\"\n---\n";

    [Fact]
    public void DesignSystemPromptSection_Over32000Chars_KeepsFrontmatterAndNamesTheCut()
    {
        var prose = string.Concat(Enumerable.Repeat("The green is the only accent on a page.\n", 1_000));
        var content = Frontmatter + "\n" + prose;

        var section = DesignSystemPromptSection.Render([Document("repo-a", content)]);

        section.Should().Contain(Frontmatter, "the tokens are exact values and are never summarised");
        section.Should().Contain("repo-a/DESIGN.md continues for")
            .And.Contain("read it with read_file \"repo-a/DESIGN.md\"");
        section.Length.Should().BeLessThan(content.Length);
        section.Length.Should().BeLessThanOrEqualTo(DesignSystemPromptSection.Budget + 600);
    }

    [Fact]
    public void DesignSystemPromptSection_None_RendersNothing()
    {
        DesignSystemPromptSection.Render([]).Should().BeEmpty();
        DesignSystemPromptSection.Render(new PipelineContext()).Should().BeEmpty();
    }

    [Fact]
    public void DesignSystemPromptSection_PipelineDocument_RendersVerbatimUnderItsRepository()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<ContextDocument>>(ContextKeys.DesignSystem,
            [Document("repo-a", Frontmatter + "\nInk on cream.\n")]);

        var section = DesignSystemPromptSection.Render(pipeline);

        section.Should().Contain("## Design system — repo-a")
            .And.Contain(Frontmatter + "\nInk on cream.")
            .And.NotContain("continues for");
    }

    [Fact]
    public void DesignSystemPromptSection_SecondRepositoryPastTheBudget_IsNamedNotDropped()
    {
        var large = Frontmatter + new string('x', 40) + "\n" + string.Concat(Enumerable.Repeat("prose line\n", 4_000));

        var section = DesignSystemPromptSection.Render([Document("repo-a", large), Document("repo-b", Frontmatter)]);

        section.Should().Contain("## Design system — repo-b")
            .And.Contain("repo-b/DESIGN.md continues for");
    }

    private static ContextDocument Document(string repo, string content) =>
        new(repo, null, null, "DESIGN.md", content);
}
