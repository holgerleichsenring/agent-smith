using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Prompts;

/// <summary>
/// 2026-11-09-0620c: each master's last words are written for its role — a report for mad-discussion and legal
/// analysis, a diff to judge for pr-review, and its own job for a test or doc pass.
/// </summary>
public sealed class MasterRolePromptTests
{
    [Fact]
    public void MasterClosing_MadDiscussion_AsksForTheReportNotACodeChange()
    {
        var closing = MasterClosing.For("mad-discussion-master", new PipelineContext());

        closing.Should().Contain("the report your methodology names, written where it says")
            .And.NotContain("nothing in the repositories needs to change").And.NotContain("build + tests");
    }

    [Fact]
    public void MasterClosing_Legal_NamesTheConvertedDocumentAndItsType()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.DocumentMarkdownPath, "contract.md");
        pipeline.Set(ContextKeys.ContractType, "nda");

        MasterClosing.For("legal-analyst-master", pipeline).Should().Contain("`contract.md`, from the repository root")
            .And.Contain("`nda`").And.NotContain("implement it");
    }

    [Fact]
    public void MasterClosing_Coding_NamesWhatIsParsedNotAProcedure()
    {
        MasterClosing.For("coding-agent-master", new PipelineContext()).Should()
            .Contain("your verdict block, or, when you asked with ask_human, the question")
            .And.NotContain("plan your change");
    }

    [Fact]
    public void ScanMasterPrompt_PrReview_JudgesTheDiffWithoutTheSecurityHeader()
    {
        var repo = new Repository(new BranchName("feature/x"), "https://example.invalid/r.git");

        var prompt = new ScanMasterPromptFactory().Build(new PipelineContext(), repo, ["repo"], "pr-review-master");

        prompt.Should().Contain("## Working source").And.Contain("This is a review of a pull request's change")
            .And.NotContain("This is a security review").And.NotContain("inventory the surface yourself");
    }

    [Fact]
    public void ScanReadFloor_PrReview_IsZero()
    {
        var agent = new AgentConfig { ScanMinSourceReads = 6 };

        ScanReadFloor.Of("pr-review-master", agent).Should().Be(0);
        ScanReadFloor.Of("security-master", agent).Should().Be(6);
    }

    [Fact]
    public void ChangePassSystemPrompt_Tests_StatesItsJobWithoutTheCodingMaster()
    {
        var prompt = new AgentPromptBuilder().BuildChangePassSystemPrompt(ChangePass.Tests, "PRINCIPLES", null);

        prompt.Should().StartWith("## Coding Principles\nPRINCIPLES", "the coding master's order, so the prefix is shared")
            .And.Contain("You add tests for a change another pass of this run already made")
            .And.NotContain("{").And.NotContain("attempts");
    }
}
