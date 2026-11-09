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

        closing.Should().Contain("the report your methodology names").And.NotContain("implement it")
            .And.NotContain("build + tests");
    }

    [Fact]
    public void MasterClosing_Legal_NamesTheConvertedDocumentAndItsType()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.DocumentMarkdownPath, "/work/contract.md");
        pipeline.Set(ContextKeys.ContractType, "nda");

        MasterClosing.For("legal-analyst-master", pipeline).Should().Contain("`/work/contract.md`")
            .And.Contain("`nda`").And.NotContain("implement it");
    }

    [Fact]
    public void MasterClosing_Coding_IsUnchanged()
    {
        MasterClosing.For("coding-agent-master", new PipelineContext()).Should()
            .StartWith("Investigate the repositories, plan your change, implement it");
    }

    [Fact]
    public void ScanMasterPrompt_PrReview_JudgesTheDiffWithoutTheSecurityHeader()
    {
        var repo = new Repository(new BranchName("feature/x"), "https://example.invalid/r.git");

        var prompt = new ScanMasterPromptFactory().Build(new PipelineContext(), repo, ["repo"], "pr-review-master");

        prompt.Should().Contain("## The checkout").And.Contain("Judge the change the diff above shows")
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

        prompt.Should().StartWith("You add tests for a change another pass of this run already made")
            .And.Contain("## Coding Principles\nPRINCIPLES").And.NotContain("{").And.NotContain("attempts");
    }
}
