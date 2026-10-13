using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Lifecycle;
using FluentAssertions;

namespace AgentSmith.Tests.Commands;

/// <summary>2026-10-12-3a36c: the one message a failed run commits its work under.</summary>
public sealed class WipCommitMessageTests
{
    [Fact]
    public void Build_NamesRunPipelineAndFailedStep_UnderTheWipSubject()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "run-1");
        pipeline.Set(ContextKeys.FailedStepName, "[4/9] AgenticExecute");
        pipeline.Set(ContextKeys.ResolvedPipeline, new ResolvedPipelineConfig("code", new AgentConfig(), "/skills", null));

        var message = new WipCommitMessage().Build(pipeline);

        message.Should().StartWith(WipCommit.Subject("run-1"));
        message.Should().Be(
            "[wip] agent-smith run run-1\n\nRun-Id: run-1\nPipeline: code\nFailed-Step: [4/9] AgenticExecute\n");
    }

    [Fact]
    public void Build_NothingKnown_SaysUnknown()
    {
        var message = new WipCommitMessage().Build(new PipelineContext());

        message.Should().Contain("Run-Id: unknown").And.Contain("Pipeline: unknown").And.Contain("Failed-Step: unknown");
    }
}
