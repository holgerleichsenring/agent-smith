using AgentSmith.Application.Services.Browser;
using AgentSmith.Application.Services.Resume;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Browser;

/// <summary>
/// 2026-10-01-283de: a design turn seeds its project for render_reference, the seed is live state a
/// checkpoint never carries, and a render script that wrote no result is reported, not parsed.
/// </summary>
public sealed class RenderReferenceSeedTests
{
    [Fact]
    public void TurnSeeds_CarryTheProject_ForTheBrowserSandboxSpec()
    {
        var project = new ResolvedProject { Name = "p" };

        var seeds = SpecDialogTurnSeeds.Build(
            new ConversationState
            {
                JobId = "s-1", Project = "p", ChannelId = "d-1", UserId = "person-a",
                Platform = "dashboard", TicketId = string.Empty, StartedAt = DateTimeOffset.UnixEpoch,
            },
            [new RepoConnection { Name = "sample-api" }],
            new Dictionary<string, ISandbox>(), new SpecDialogReplySlot(),
            DialogImageSet.None, Mock.Of<Contracts.Dialogue.IFiledTicketWithdrawal>(), project: project);

        seeds[ContextKeys.SpecDialogProject].Should().BeSameAs(project);
    }

    [Fact]
    public void PipelineContextSerializer_ProjectSeed_IsNotCheckpointed()
    {
        var sut = new PipelineContextSerializer(NullLogger<PipelineContextSerializer>.Instance);
        var source = new PipelineContext();
        source.Set(ContextKeys.RunId, "run-1");
        source.Set(ContextKeys.SpecDialogProject, new ResolvedProject { Name = "p" });

        var target = new PipelineContext();
        sut.Restore(sut.Serialize(source), target);

        target.Has(ContextKeys.SpecDialogProject).Should().BeFalse("re-resolved from the config on every turn");
    }

    [Fact]
    public async Task BrowserRenderInvocation_ScriptWroteNoResult_ReportsTheExitAndTheError()
    {
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(It.IsAny<Step>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Step step, IProgress<StepEvent>? _, CancellationToken _) => step.Kind == StepKind.Run
                ? new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 127, false, 0.1, "node: not found")
                : step.Kind == StepKind.ReadFile
                    ? new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 1, false, 0.1, "file not found")
                    : new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null));
        var request = new BrowserRenderRequest("https://example.test/", null, "", ["body"], ["color"], "/work/render/x");

        var (result, shots, failure) = await new BrowserRenderInvocation(
            new AgentSmith.Application.Services.Sandbox.SandboxFileReaderFactory()).RunAsync(sandbox.Object, request, CancellationToken.None);

        result.Should().BeNull();
        shots.Should().BeEmpty();
        failure.Should().Contain("exit 127").And.Contain("node: not found");
    }
}
