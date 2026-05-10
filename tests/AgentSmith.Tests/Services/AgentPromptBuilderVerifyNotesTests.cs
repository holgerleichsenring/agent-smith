using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0129a: AgenticExecuteHandler reads ContextKeys.VerifyNotes and threads it through
/// to BuildExecutionUserPrompt. The builder either prepends a "Prior verify-phase
/// observations" section or omits it entirely when notes are absent / blank.
/// </summary>
public sealed class AgentPromptBuilderVerifyNotesTests
{
    private readonly AgentPromptBuilder _sut;

    public AgentPromptBuilderVerifyNotesTests()
    {
        var prompts = new Mock<IPromptCatalog>();
        prompts.Setup(p => p.Render(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>()))
               .Returns("system-stub");
        _sut = new AgentPromptBuilder(prompts.Object);
    }

    [Fact]
    public void BuildExecutionUserPrompt_NoVerifyNotes_PromptHasNoVerifySection()
    {
        var plan = SamplePlan();
        var repo = SampleRepo();

        var prompt = _sut.BuildExecutionUserPrompt(plan, repo);

        prompt.Should().NotContain("Prior verify-phase observations");
    }

    [Fact]
    public void BuildExecutionUserPrompt_VerifyNotesPresent_PromptIncludesNotesSection()
    {
        var plan = SamplePlan();
        var repo = SampleRepo();
        var notes = "## Verify round 1: 1 blocking observation(s)\n- [high] **Scope** (x.cs): out of scope";

        var prompt = _sut.BuildExecutionUserPrompt(plan, repo, notes);

        prompt.Should().Contain("Prior verify-phase observations");
        prompt.Should().Contain("Apply these to the next implementation pass");
        prompt.Should().Contain("Verify round 1");
    }

    [Fact]
    public void BuildExecutionUserPrompt_VerifyNotesEmptyString_NoSection()
    {
        var prompt = _sut.BuildExecutionUserPrompt(SamplePlan(), SampleRepo(), "");

        prompt.Should().NotContain("Prior verify-phase observations");
    }

    [Fact]
    public void BuildExecutionUserPrompt_VerifyNotesWhitespace_NoSection()
    {
        var prompt = _sut.BuildExecutionUserPrompt(SamplePlan(), SampleRepo(), "   \n  ");

        prompt.Should().NotContain("Prior verify-phase observations");
    }

    private static Plan SamplePlan() => new(
        "fix it",
        new[] { new PlanStep(1, "Add field", new FilePath("x.cs"), "Modify") },
        "{}");

    private static Repository SampleRepo() =>
        new(new BranchName("agent-smith/feat/1"), "https://example/repo");
}
