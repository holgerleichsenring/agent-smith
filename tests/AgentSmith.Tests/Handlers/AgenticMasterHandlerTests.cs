using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net.Http;

namespace AgentSmith.Tests.Handlers;

public sealed class AgenticMasterHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_ResolvesMasterBody_AndPassesItAsSystemPrompt()
    {
        const string masterName = "coding-agent-master";
        const string masterBody = "## Role\nYou are the coding master. Plan, execute, verify.";
        var prompts = new MasterHandlerFixture.StubPromptCatalog(name: masterName, body: masterBody);
        var loop = new CapturingLoopRunner();

        var sut = MasterHandlerFixture.Build(loop, prompts);
        var context = MasterHandlerFixture.BuildContext(masterName);

        var result = await sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        loop.SeenRequests.Should().ContainSingle();
        loop.SeenRequests[0].SystemPrompt.Should().Be(masterBody);
    }

    [Fact]
    public async Task ExecuteAsync_PassesFullToolSurface_ReadWriteHumanLog()
    {
        var prompts = new MasterHandlerFixture.StubPromptCatalog("coding-agent-master", "body");
        var loop = new CapturingLoopRunner();

        await MasterHandlerFixture.Build(loop, prompts).ExecuteAsync(MasterHandlerFixture.BuildContext("coding-agent-master"), CancellationToken.None);

        var toolNames = loop.SeenRequests[0].Tools.OfType<AIFunction>().Select(t => t.Name).ToHashSet();
        toolNames.Should().Contain("read_file");
        toolNames.Should().Contain("write_file");
        toolNames.Should().Contain("log_decision");
        toolNames.Should().Contain("ask_human");
    }

    [Fact]
    public async Task ExecuteAsync_SetsCodeChangesAndDurationInPipelineContext()
    {
        var prompts = new MasterHandlerFixture.StubPromptCatalog("coding-agent-master", "body");
        var loop = new CapturingLoopRunner();

        var context = MasterHandlerFixture.BuildContext("coding-agent-master");
        await MasterHandlerFixture.Build(loop, prompts).ExecuteAsync(context, CancellationToken.None);

        context.Pipeline.TryGet<int>(ContextKeys.RunDurationSeconds, out _).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_NoTicketInPipeline_StillRuns_NoPlanDependency()
    {
        var prompts = new MasterHandlerFixture.StubPromptCatalog("coding-agent-master", "body");
        var loop = new CapturingLoopRunner();

        var ctx = MasterHandlerFixture.BuildContext("coding-agent-master", includeTicket: false);

        var result = await MasterHandlerFixture.Build(loop, prompts).ExecuteAsync(ctx, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        loop.SeenRequests[0].UserPrompt.Should().Contain("(No ticket attached");
    }

    [Fact]
    public async Task ExecuteAsync_RendersTokenSubstitutionThroughPromptCatalog()
    {
        // Master body templates may contain {CodingPrinciples} / {ProjectContextSection}
        // / {CodeMapSection}. The handler must call Render, not raw Get.
        var prompts = new MasterHandlerFixture.StubPromptCatalog("coding-agent-master",
            "Principles:{CodingPrinciples}|Context:{ProjectContextSection}|Map:{CodeMapSection}");
        var loop = new CapturingLoopRunner();

        var ctx = MasterHandlerFixture.BuildContext("coding-agent-master", codingPrinciples: "RULES");
        await MasterHandlerFixture.Build(loop, prompts).ExecuteAsync(ctx, CancellationToken.None);

        loop.SeenRequests[0].SystemPrompt.Should().Contain("Principles:RULES");
    }

    [Fact]
    public async Task ExecuteAsync_NoVerdictOnGreenTestsPipeline_RePromptsOnce_AndParsesVerdict()
    {
        // p0263: the master changed source but emitted no verdict on a green-tests
        // pipeline → re-prompt once for the verdict; the verdict from the nudge pass is
        // honored. (The apply-drive may fire first since the mock reports no changes;
        // the LAST request is the verdict-nudge — assert on its prompt + the parsed verdict.)
        const string verdictBlock =
            "Done.\n```verdict\n{ \"status\": \"green\", \"build_ran\": true, "
            + "\"build_passed\": true, \"tests_ran\": true, \"tests_passed\": true, "
            + "\"summary\": \"ok\" }\n```";
        var prompts = new MasterHandlerFixture.StubPromptCatalog("coding-agent-master", "body");
        var loop = new SequencedLoopRunner("no verdict here", "still nothing", verdictBlock);

        var ctx = MasterHandlerFixture.BuildContext("coding-agent-master");
        ctx.Pipeline.Set(ContextKeys.PipelineName, "fix-bug");

        await MasterHandlerFixture.Build(loop, prompts).ExecuteAsync(ctx, CancellationToken.None);

        ctx.Pipeline.TryGet<MasterVerification>(ContextKeys.MasterVerification, out var v).Should().BeTrue();
        v!.Status.Should().Be(VerificationStatus.Green);
        loop.SeenRequests[^1].UserPrompt.Should().Contain("did NOT emit the required Phase 4 verdict");
    }

    [Fact]
    public async Task AgenticMaster_ScanMaster_UsesReviewPromptAndReadOnlyTools()
    {
        var prompts = new MasterHandlerFixture.StubPromptCatalog("api-security-master", "## Role\nreviewer");
        var loop = new CapturingLoopRunner();

        await MasterHandlerFixture.Build(loop, prompts, masterSchema: "observation")
            .ExecuteAsync(MasterHandlerFixture.BuildContext("api-security-master"), CancellationToken.None);

        var tools = loop.SeenRequests[0].Tools.OfType<AIFunction>().Select(t => t.Name).ToHashSet();
        tools.Should().Contain("read_file").And.Contain("log_decision");
        tools.Should().NotContain("write_file");
        tools.Should().NotContain("edit");
        tools.Should().NotContain("run_command");
        tools.Should().NotContain("ask_human");
        loop.SeenRequests[0].UserPrompt.Should().Contain("SECURITY REVIEW");
        loop.SeenRequests[0].UserPrompt.Should().Contain("observation array");
    }

    [Fact]
    public async Task AgenticMaster_CodingMaster_UsesCodingPromptAndReadWriteTools_Unchanged()
    {
        var prompts = new MasterHandlerFixture.StubPromptCatalog("coding-agent-master", "body");
        var loop = new CapturingLoopRunner();

        // masterSchema null → not a scan master → the existing coding path.
        await MasterHandlerFixture.Build(loop, prompts).ExecuteAsync(MasterHandlerFixture.BuildContext("coding-agent-master"), CancellationToken.None);

        var tools = loop.SeenRequests[0].Tools.OfType<AIFunction>().Select(t => t.Name).ToHashSet();
        tools.Should().Contain("write_file").And.Contain("run_command").And.Contain("ask_human");
        loop.SeenRequests[0].UserPrompt.Should().Contain("implement");
    }

    [Fact]
    public async Task AgenticMaster_ScanMaster_DoesNotDriveApplyOrVerdictNudge()
    {
        // A scan master changes nothing and emits no verdict; it must NOT trigger the
        // apply-drive or verdict-nudge re-prompts (those are coding-pipeline salvage).
        var prompts = new MasterHandlerFixture.StubPromptCatalog("api-security-master", "body");
        var loop = new CapturingLoopRunner();
        // floor 0 isolates this from p0279's coverage re-drive — we assert only that the
        // apply-drive / verdict-nudge (coding salvage) never fire for a scan master.
        var ctx = MasterHandlerFixture.BuildContext("api-security-master", scanMinSourceReads: 0);
        ctx.Pipeline.Set(ContextKeys.PipelineName, "api-security-scan");

        await MasterHandlerFixture.Build(loop, prompts, masterSchema: "observation").ExecuteAsync(ctx, CancellationToken.None);

        loop.SeenRequests.Should().ContainSingle("no apply/verdict re-prompt (coverage re-drive disabled by floor 0)");
    }

    [Fact]
    public async Task AgenticMaster_ScanMaster_BelowReadFloor_RePromptsOnceForCoverage()
    {
        // CapturingLoopRunner calls no read tools, so the read-set is empty (< default
        // floor 6) → the scan master is re-driven once with the coverage nudge.
        var prompts = new MasterHandlerFixture.StubPromptCatalog("api-security-master", "body");
        var loop = new CapturingLoopRunner();

        await MasterHandlerFixture.Build(loop, prompts, masterSchema: "observation")
            .ExecuteAsync(MasterHandlerFixture.BuildContext("api-security-master"), CancellationToken.None);

        loop.SeenRequests.Should().HaveCount(2, "0 reads is below the floor → one coverage re-drive");
        loop.SeenRequests[1].UserPrompt.Should().Contain("FULL surface");
    }

    [Fact]
    public async Task AgenticMaster_ScanMaster_AboveReadFloor_DoesNotRePrompt()
    {
        // floor 0 → 0 reads is not below it → no re-drive.
        var prompts = new MasterHandlerFixture.StubPromptCatalog("api-security-master", "body");
        var loop = new CapturingLoopRunner();

        await MasterHandlerFixture.Build(loop, prompts, masterSchema: "observation")
            .ExecuteAsync(MasterHandlerFixture.BuildContext("api-security-master", scanMinSourceReads: 0), CancellationToken.None);

        loop.SeenRequests.Should().ContainSingle("at/above the floor → no coverage re-drive");
    }

    [Fact]
    public async Task AgenticMaster_CodingMaster_NeverCoverageReDriven_Unchanged()
    {
        // A coding master (schema null) never enters the scan branch, so the coverage
        // re-drive cannot fire regardless of read count.
        var prompts = new MasterHandlerFixture.StubPromptCatalog("coding-agent-master", "body");
        var loop = new CapturingLoopRunner();

        await MasterHandlerFixture.Build(loop, prompts).ExecuteAsync(MasterHandlerFixture.BuildContext("coding-agent-master"), CancellationToken.None);

        loop.SeenRequests.Should().ContainSingle("coding masters are never coverage-re-driven");
    }

    [Fact]
    public async Task AgenticMaster_ScanMaster_SubAgentsEnabled_HasSpawnAndReadObservations_ChildrenReadOnly()
    {
        var prompts = new MasterHandlerFixture.StubPromptCatalog("api-security-master", "body");
        var loop = new CapturingLoopRunner();

        await MasterHandlerFixture.Build(loop, prompts, masterSchema: "observation", maxSubAgents: 20)
            .ExecuteAsync(MasterHandlerFixture.BuildContext("api-security-master", scanMinSourceReads: 0), CancellationToken.None);

        var tools = loop.SeenRequests[0].Tools.OfType<AIFunction>().Select(t => t.Name).ToHashSet();
        tools.Should().Contain("spawn_agents").And.Contain("read_sub_agent_observations");
        tools.Should().Contain("read_file");
        tools.Should().NotContain("write_file", "a scan master + its children stay read-only");
        tools.Should().NotContain("run_command");
    }

    [Fact]
    public async Task AgenticMaster_CodingMaster_SubAgentsEnabled_HasSpawn_ChildrenReadWrite()
    {
        var prompts = new MasterHandlerFixture.StubPromptCatalog("coding-agent-master", "body");
        var loop = new CapturingLoopRunner();

        await MasterHandlerFixture.Build(loop, prompts, maxSubAgents: 20)
            .ExecuteAsync(MasterHandlerFixture.BuildContext("coding-agent-master"), CancellationToken.None);

        var tools = loop.SeenRequests[0].Tools.OfType<AIFunction>().Select(t => t.Name).ToHashSet();
        tools.Should().Contain("spawn_agents").And.Contain("read_sub_agent_observations");
        tools.Should().Contain("write_file").And.Contain("run_command", "coding children can write + run");
    }

    [Fact]
    public async Task AgenticMaster_SubAgentsDisabled_NoSpawnTool()
    {
        var prompts = new MasterHandlerFixture.StubPromptCatalog("coding-agent-master", "body");
        var loop = new CapturingLoopRunner();

        await MasterHandlerFixture.Build(loop, prompts, maxSubAgents: 0)
            .ExecuteAsync(MasterHandlerFixture.BuildContext("coding-agent-master"), CancellationToken.None);

        var tools = loop.SeenRequests[0].Tools.OfType<AIFunction>().Select(t => t.Name).ToHashSet();
        tools.Should().NotContain("spawn_agents");
        tools.Should().NotContain("read_sub_agent_observations");
    }

    [Fact]
    public void ReviewToolSurface_HasReadOnlyFsAndLogDecision_NoWriteOrRun()
    {
        var fs = new AgentSmith.Application.Services.Tools.FilesystemToolHost(new Mock<ISandbox>().Object);
        var log = new AgentSmith.Application.Services.Tools.LogDecisionToolHost(new MasterHandlerFixture.NoOpDecisionLogger());

        var tools = new AgentSmith.Application.Services.Tools.AgenticToolSurface().Review(fs, log)
            .OfType<AIFunction>().Select(t => t.Name).ToHashSet();

        tools.Should().Contain("read_file").And.Contain("log_decision");
        tools.Should().NotContain("write_file");
        tools.Should().NotContain("edit");
        tools.Should().NotContain("run_command");
    }


    [Fact]
    public async Task MasterPrompt_ImagesWithVisionModel_AttachedAsContentParts()
    {
        var prompts = new MasterHandlerFixture.StubPromptCatalog("coding-agent-master", "body");
        var loop = new CapturingLoopRunner();
        var ctx = MasterHandlerFixture.BuildContext("coding-agent-master"); // supports_vision defaults to true
        ctx.Pipeline.Set<IReadOnlyList<TicketImageAttachment>>(ContextKeys.Attachments,
            [new TicketImageAttachment(new AttachmentRef("u", "shot.png", "image/png"), [1, 2, 3])]);

        await MasterHandlerFixture.Build(loop, prompts).ExecuteAsync(ctx, CancellationToken.None);

        var parts = loop.SeenRequests[0].UserImageParts;
        parts.Should().NotBeNull().And.ContainSingle();
        var image = parts![0].Should().BeOfType<DataContent>().Subject;
        image.MediaType.Should().Be("image/png");
        loop.SeenRequests[0].UserPrompt.Should().Contain(
            "1 ticket image(s) are attached to this message");
    }

    [Fact]
    public async Task MasterPrompt_ImagesWithoutVision_NotedNotAttached()
    {
        var prompts = new MasterHandlerFixture.StubPromptCatalog("coding-agent-master", "body");
        var loop = new CapturingLoopRunner();
        var ctx = MasterHandlerFixture.BuildContext("coding-agent-master", supportsVision: false);
        ctx.Pipeline.Set<IReadOnlyList<TicketImageAttachment>>(ContextKeys.Attachments,
            [new TicketImageAttachment(new AttachmentRef("u", "shot.png", "image/png"), [1, 2, 3])]);

        await MasterHandlerFixture.Build(loop, prompts).ExecuteAsync(ctx, CancellationToken.None);

        loop.SeenRequests[0].UserImageParts.Should().BeNullOrEmpty();
        loop.SeenRequests[0].UserPrompt.Should().Contain("not viewable");
    }

    [Fact]
    public async Task MasterPrompt_TicketComments_RenderedIntoUserPrompt()
    {
        var prompts = new MasterHandlerFixture.StubPromptCatalog("coding-agent-master", "body");
        var loop = new CapturingLoopRunner();
        var ctx = MasterHandlerFixture.BuildContext("coding-agent-master");
        ctx.Pipeline.Set<IReadOnlyList<TicketComment>>(ContextKeys.TicketComments,
            [new TicketComment("jane", DateTimeOffset.UtcNow, "use approach B, not A")]);

        await MasterHandlerFixture.Build(loop, prompts).ExecuteAsync(ctx, CancellationToken.None);

        loop.SeenRequests[0].UserPrompt.Should().Contain("## Ticket conversation");
        loop.SeenRequests[0].UserPrompt.Should().Contain("use approach B, not A");
    }

    /// <summary>
    /// p0469: p0452 published the agent's commands from the budget catch, the general catch
    /// and the mid-run-question park — every path EXCEPT the one a phase normally takes. A
    /// completing phase handed the account nothing the agent ran, and a live run was refused
    /// on "no listed command provides an exhaustive scan" over a scan the agent had run
    /// several times. The log is published before the loop opens, so completing publishes it.
    /// </summary>
    [Fact]
    public async Task PhaseCommands_MasterCompletesNormally_ReachTheAccount()
    {
        var prompts = new MasterHandlerFixture.StubPromptCatalog("coding-agent-master", "body");
        var ctx = MasterHandlerFixture.BuildContext("coding-agent-master");
        var loop = new SearchingLoopRunner(ctx.Pipeline, "grep -rn 'Sample' src");

        var result = await MasterHandlerFixture.Build(loop, prompts).ExecuteAsync(ctx, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        AgentSmith.Application.Services.Specs.PhaseEvidence.From([], ctx.Pipeline)
            .Should().ContainSingle().Which.Should().Contain("grep -rn 'Sample' src",
                "a phase that finished normally hands the account every command the agent ran");
    }

    /// <summary>
    /// p0469: the repair pass p0438 splices is a SECOND master pass in the same phase, and
    /// the handler builds a new tool host per pass. With the log owned by the host, the
    /// repair started empty and published over the first pass's searches — the ones that
    /// prove an absence, which the agent runs early.
    /// </summary>
    [Fact]
    public async Task PhaseCommands_SecondMasterPassInOnePhase_KeepsTheFirstPassCommands()
    {
        var prompts = new MasterHandlerFixture.StubPromptCatalog("coding-agent-master", "body");
        var ctx = MasterHandlerFixture.BuildContext("coding-agent-master");
        var loop = new SearchingLoopRunner(ctx.Pipeline, "grep -rn 'Sample' src", "dotnet test");
        var sut = MasterHandlerFixture.Build(loop, prompts);

        await sut.ExecuteAsync(ctx, CancellationToken.None);
        await sut.ExecuteAsync(ctx, CancellationToken.None);

        var evidence = AgentSmith.Application.Services.Specs.PhaseEvidence.From([], ctx.Pipeline);
        evidence.Should().HaveCount(2);
        evidence[0].Should().Contain("grep -rn 'Sample' src", "the repair pass adds to the phase's evidence");
        evidence[1].Should().Contain("dotnet test");
    }

    private sealed class CapturingLoopRunner : IAgenticLoopRunner
    {
        private readonly List<AgenticLoopRequest> _seen = new();
        public IReadOnlyList<AgenticLoopRequest> SeenRequests => _seen;

        public Task<AgenticLoopResult> RunAsync(AgenticLoopRequest request, CancellationToken cancellationToken)
        {
            _seen.Add(request);
            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok"))
            {
                Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5 },
            };
            return Task.FromResult(new AgenticLoopResult(response, TimeSpan.FromSeconds(1)));
        }
    }

    // p0263: returns a scripted response text per call (last text repeats if exhausted),
    // so a test can drive "no verdict → no verdict → verdict on the nudge pass".
    private sealed class SequencedLoopRunner(params string[] texts) : IAgenticLoopRunner
    {
        private readonly List<AgenticLoopRequest> _seen = new();
        private int _call;
        public IReadOnlyList<AgenticLoopRequest> SeenRequests => _seen;

        public Task<AgenticLoopResult> RunAsync(AgenticLoopRequest request, CancellationToken cancellationToken)
        {
            _seen.Add(request);
            var text = texts[System.Math.Min(_call, texts.Length - 1)];
            _call++;
            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, text))
            {
                Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5 },
            };
            return Task.FromResult(new AgenticLoopResult(response, TimeSpan.FromSeconds(1)));
        }
    }

    // p0469: stands in for the master calling run_command — the tool host records into the
    // log the handler published for the phase, so a test that never reaches a sandbox still
    // proves the publication happens BEFORE the loop and survives the pass.
    private sealed class SearchingLoopRunner(
        PipelineContext pipeline, params string[] commands) : IAgenticLoopRunner
    {
        private int _call;

        public Task<AgenticLoopResult> RunAsync(AgenticLoopRequest request, CancellationToken cancellationToken)
        {
            pipeline.Get<AgentSmith.Application.Services.Specs.PhaseCommandLog>(ContextKeys.PhaseCommands)
                .Record("api", commands[System.Math.Min(_call++, commands.Length - 1)], "exit_code: 1\n\nstdout:\n");
            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok"))
            {
                Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5 },
            };
            return Task.FromResult(new AgenticLoopResult(response, TimeSpan.FromSeconds(1)));
        }
    }
}
