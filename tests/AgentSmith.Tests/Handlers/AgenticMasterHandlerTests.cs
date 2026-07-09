using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Loop;
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
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

public sealed class AgenticMasterHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_ResolvesMasterBody_AndPassesItAsSystemPrompt()
    {
        const string masterName = "coding-agent-master";
        const string masterBody = "## Role\nYou are the coding master. Plan, execute, verify.";
        var prompts = new StubPromptCatalog(name: masterName, body: masterBody);
        var loop = new CapturingLoopRunner();

        var sut = Build(loop, prompts);
        var context = BuildContext(masterName);

        var result = await sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        loop.SeenRequests.Should().ContainSingle();
        loop.SeenRequests[0].SystemPrompt.Should().Be(masterBody);
    }

    [Fact]
    public async Task ExecuteAsync_PassesFullToolSurface_ReadWriteHumanLog()
    {
        var prompts = new StubPromptCatalog("coding-agent-master", "body");
        var loop = new CapturingLoopRunner();

        await Build(loop, prompts).ExecuteAsync(BuildContext("coding-agent-master"), CancellationToken.None);

        var toolNames = loop.SeenRequests[0].Tools.OfType<AIFunction>().Select(t => t.Name).ToHashSet();
        toolNames.Should().Contain("read_file");
        toolNames.Should().Contain("write_file");
        toolNames.Should().Contain("log_decision");
        toolNames.Should().Contain("ask_human");
    }

    [Fact]
    public async Task ExecuteAsync_SetsCodeChangesAndDurationInPipelineContext()
    {
        var prompts = new StubPromptCatalog("coding-agent-master", "body");
        var loop = new CapturingLoopRunner();

        var context = BuildContext("coding-agent-master");
        await Build(loop, prompts).ExecuteAsync(context, CancellationToken.None);

        context.Pipeline.TryGet<int>(ContextKeys.RunDurationSeconds, out _).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_NoTicketInPipeline_StillRuns_NoPlanDependency()
    {
        var prompts = new StubPromptCatalog("coding-agent-master", "body");
        var loop = new CapturingLoopRunner();

        var ctx = BuildContext("coding-agent-master", includeTicket: false);

        var result = await Build(loop, prompts).ExecuteAsync(ctx, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        loop.SeenRequests[0].UserPrompt.Should().Contain("(No ticket attached");
    }

    [Fact]
    public async Task ExecuteAsync_RendersTokenSubstitutionThroughPromptCatalog()
    {
        // Master body templates may contain {CodingPrinciples} / {ProjectContextSection}
        // / {CodeMapSection}. The handler must call Render, not raw Get.
        var prompts = new StubPromptCatalog("coding-agent-master",
            "Principles:{CodingPrinciples}|Context:{ProjectContextSection}|Map:{CodeMapSection}");
        var loop = new CapturingLoopRunner();

        var ctx = BuildContext("coding-agent-master", codingPrinciples: "RULES");
        await Build(loop, prompts).ExecuteAsync(ctx, CancellationToken.None);

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
        var prompts = new StubPromptCatalog("coding-agent-master", "body");
        var loop = new SequencedLoopRunner("no verdict here", "still nothing", verdictBlock);

        var ctx = BuildContext("coding-agent-master");
        ctx.Pipeline.Set(ContextKeys.PipelineName, "fix-bug");

        await Build(loop, prompts).ExecuteAsync(ctx, CancellationToken.None);

        ctx.Pipeline.TryGet<MasterVerification>(ContextKeys.MasterVerification, out var v).Should().BeTrue();
        v!.Status.Should().Be(VerificationStatus.Green);
        loop.SeenRequests[^1].UserPrompt.Should().Contain("did NOT emit the required Phase 4 verdict");
    }

    [Fact]
    public async Task AgenticMaster_ScanMaster_UsesReviewPromptAndReadOnlyTools()
    {
        var prompts = new StubPromptCatalog("api-security-master", "## Role\nreviewer");
        var loop = new CapturingLoopRunner();

        await Build(loop, prompts, masterSchema: "observation")
            .ExecuteAsync(BuildContext("api-security-master"), CancellationToken.None);

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
        var prompts = new StubPromptCatalog("coding-agent-master", "body");
        var loop = new CapturingLoopRunner();

        // masterSchema null → not a scan master → the existing coding path.
        await Build(loop, prompts).ExecuteAsync(BuildContext("coding-agent-master"), CancellationToken.None);

        var tools = loop.SeenRequests[0].Tools.OfType<AIFunction>().Select(t => t.Name).ToHashSet();
        tools.Should().Contain("write_file").And.Contain("run_command").And.Contain("ask_human");
        loop.SeenRequests[0].UserPrompt.Should().Contain("implement");
    }

    [Fact]
    public async Task AgenticMaster_ScanMaster_DoesNotDriveApplyOrVerdictNudge()
    {
        // A scan master changes nothing and emits no verdict; it must NOT trigger the
        // apply-drive or verdict-nudge re-prompts (those are coding-pipeline salvage).
        var prompts = new StubPromptCatalog("api-security-master", "body");
        var loop = new CapturingLoopRunner();
        // floor 0 isolates this from p0279's coverage re-drive — we assert only that the
        // apply-drive / verdict-nudge (coding salvage) never fire for a scan master.
        var ctx = BuildContext("api-security-master", scanMinSourceReads: 0);
        ctx.Pipeline.Set(ContextKeys.PipelineName, "api-security-scan");

        await Build(loop, prompts, masterSchema: "observation").ExecuteAsync(ctx, CancellationToken.None);

        loop.SeenRequests.Should().ContainSingle("no apply/verdict re-prompt (coverage re-drive disabled by floor 0)");
    }

    [Fact]
    public async Task AgenticMaster_ScanMaster_BelowReadFloor_RePromptsOnceForCoverage()
    {
        // CapturingLoopRunner calls no read tools, so the read-set is empty (< default
        // floor 6) → the scan master is re-driven once with the coverage nudge.
        var prompts = new StubPromptCatalog("api-security-master", "body");
        var loop = new CapturingLoopRunner();

        await Build(loop, prompts, masterSchema: "observation")
            .ExecuteAsync(BuildContext("api-security-master"), CancellationToken.None);

        loop.SeenRequests.Should().HaveCount(2, "0 reads is below the floor → one coverage re-drive");
        loop.SeenRequests[1].UserPrompt.Should().Contain("FULL surface");
    }

    [Fact]
    public async Task AgenticMaster_ScanMaster_AboveReadFloor_DoesNotRePrompt()
    {
        // floor 0 → 0 reads is not below it → no re-drive.
        var prompts = new StubPromptCatalog("api-security-master", "body");
        var loop = new CapturingLoopRunner();

        await Build(loop, prompts, masterSchema: "observation")
            .ExecuteAsync(BuildContext("api-security-master", scanMinSourceReads: 0), CancellationToken.None);

        loop.SeenRequests.Should().ContainSingle("at/above the floor → no coverage re-drive");
    }

    [Fact]
    public async Task AgenticMaster_CodingMaster_NeverCoverageReDriven_Unchanged()
    {
        // A coding master (schema null) never enters the scan branch, so the coverage
        // re-drive cannot fire regardless of read count.
        var prompts = new StubPromptCatalog("coding-agent-master", "body");
        var loop = new CapturingLoopRunner();

        await Build(loop, prompts).ExecuteAsync(BuildContext("coding-agent-master"), CancellationToken.None);

        loop.SeenRequests.Should().ContainSingle("coding masters are never coverage-re-driven");
    }

    [Fact]
    public async Task AgenticMaster_ScanMaster_SubAgentsEnabled_HasSpawnAndReadObservations_ChildrenReadOnly()
    {
        var prompts = new StubPromptCatalog("api-security-master", "body");
        var loop = new CapturingLoopRunner();

        await Build(loop, prompts, masterSchema: "observation", maxSubAgents: 20)
            .ExecuteAsync(BuildContext("api-security-master", scanMinSourceReads: 0), CancellationToken.None);

        var tools = loop.SeenRequests[0].Tools.OfType<AIFunction>().Select(t => t.Name).ToHashSet();
        tools.Should().Contain("spawn_agents").And.Contain("read_sub_agent_observations");
        tools.Should().Contain("read_file");
        tools.Should().NotContain("write_file", "a scan master + its children stay read-only");
        tools.Should().NotContain("run_command");
    }

    [Fact]
    public async Task AgenticMaster_CodingMaster_SubAgentsEnabled_HasSpawn_ChildrenReadWrite()
    {
        var prompts = new StubPromptCatalog("coding-agent-master", "body");
        var loop = new CapturingLoopRunner();

        await Build(loop, prompts, maxSubAgents: 20)
            .ExecuteAsync(BuildContext("coding-agent-master"), CancellationToken.None);

        var tools = loop.SeenRequests[0].Tools.OfType<AIFunction>().Select(t => t.Name).ToHashSet();
        tools.Should().Contain("spawn_agents").And.Contain("read_sub_agent_observations");
        tools.Should().Contain("write_file").And.Contain("run_command", "coding children can write + run");
    }

    [Fact]
    public async Task AgenticMaster_SubAgentsDisabled_NoSpawnTool()
    {
        var prompts = new StubPromptCatalog("coding-agent-master", "body");
        var loop = new CapturingLoopRunner();

        await Build(loop, prompts, maxSubAgents: 0)
            .ExecuteAsync(BuildContext("coding-agent-master"), CancellationToken.None);

        var tools = loop.SeenRequests[0].Tools.OfType<AIFunction>().Select(t => t.Name).ToHashSet();
        tools.Should().NotContain("spawn_agents");
        tools.Should().NotContain("read_sub_agent_observations");
    }

    [Fact]
    public void ReviewToolSurface_HasReadOnlyFsAndLogDecision_NoWriteOrRun()
    {
        var fs = new AgentSmith.Application.Services.Tools.FilesystemToolHost(new Mock<ISandbox>().Object);
        var log = new AgentSmith.Application.Services.Tools.LogDecisionToolHost(new NoOpDecisionLogger());

        var tools = AgentSmith.Application.Services.Tools.AgenticToolSurface.Review(fs, log)
            .OfType<AIFunction>().Select(t => t.Name).ToHashSet();

        tools.Should().Contain("read_file").And.Contain("log_decision");
        tools.Should().NotContain("write_file");
        tools.Should().NotContain("edit");
        tools.Should().NotContain("run_command");
    }

    private static AgenticMasterHandler Build(
        IAgenticLoopRunner loop, IPromptCatalog prompts, string? masterSchema = null,
        int maxSubAgents = 0) =>
        new(loop, prompts, new NoOpDecisionLogger(), AgentSmithConfig.Empty(),
            new AgentSmith.Infrastructure.Services.ContextYamlSerializer(),
            new StubSchemaResolver(masterSchema),
            new AgentSmith.Application.Services.ScanMasterPromptFactory(),
            new AgentSmith.Application.Services.SpecDialogPromptFactory(),
            new AgentSmith.Application.Services.PhaseExecutionPromptFactory(),
            BuildOutcomeResolver(),
            new StubSubAgentRunner(),
            new SubAgentBudget(20),
            new SubAgentNameValidator(),
            new InMemoryChildAnswerStore(),
            new LoopLimitsConfig { MaxSubAgentsPerRun = maxSubAgents },
            dialogueTransport: null, NullLogger<AgenticMasterHandler>.Instance);

    // p0315e: the real resolver chain over the real schema — the handler gate
    // now resolves a typed outcome instead of validating only the draft.
    private static AgentSmith.Application.Services.SpecDialog.OutcomeProposalResolver BuildOutcomeResolver()
    {
        var validator = new AgentSmith.Application.Services.SpecDialog.SpecDraftValidator(
            new AgentSmith.Application.Services.Validation.PhaseSpecSchemaProvider());
        var reader = new AgentSmith.Application.Services.SpecDialog.PhaseDraftReader();
        return new AgentSmith.Application.Services.SpecDialog.OutcomeProposalResolver(
            validator, reader,
            new AgentSmith.Application.Services.SpecDialog.BugOutcomeParser(),
            new AgentSmith.Application.Services.SpecDialog.EpicOutcomeParser(
                validator, reader,
                new AgentSmith.Application.Services.SpecDialog.RequiresEdgeChecker()));
    }

    private sealed class StubSchemaResolver(string? schema) : IMasterOutputSchemaResolver
    {
        public string? Resolve(string masterSkillName) => schema;
    }

    private sealed class StubSubAgentRunner : ISubAgentRunner
    {
        public Task<IReadOnlyList<SubAgentResult>> RunAsync(
            IReadOnlyList<SubAgentSpec> specs, SubAgentContext context, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<SubAgentResult>>([]);
    }

    private static AgenticMasterContext BuildContext(
        string masterSkillName,
        bool includeTicket = true,
        string codingPrinciples = "principles",
        int scanMinSourceReads = 6)
    {
        var pipeline = new PipelineContext();
        var sandboxes = new Dictionary<string, ISandbox>(StringComparer.Ordinal)
        {
            ["default"] = new Mock<ISandbox>().Object,
        };
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, sandboxes);
        if (includeTicket)
        {
            pipeline.Set(ContextKeys.Ticket,
                new Ticket(
                    id: new TicketId("TKT-1"),
                    title: "Test ticket",
                    description: "Do the thing",
                    acceptanceCriteria: null,
                    status: "open",
                    source: "test"));
        }
        var repo = new Repository(new BranchName("feature/x"), "https://example.test/repo.git");
        return new AgenticMasterContext(
            MasterSkillName: masterSkillName,
            Repository: repo,
            CodingPrinciples: codingPrinciples,
            AgentConfig: new AgentConfig { ScanMinSourceReads = scanMinSourceReads },
            Pipeline: pipeline);
    }

    private sealed class StubPromptCatalog(string name, string body) : IPromptCatalog
    {
        public string Get(string n) =>
            n == name ? body : throw new InvalidOperationException($"unexpected name {n}");
        public string Render(string n, IReadOnlyDictionary<string, string> tokens)
        {
            var c = Get(n);
            foreach (var (k, v) in tokens) c = c.Replace("{" + k + "}", v);
            return c;
        }
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

    private sealed class NoOpDecisionLogger : IDecisionLogger
    {
        public Task LogAsync(string? repoPath, DecisionCategory category, string decision,
            CancellationToken cancellationToken = default, string? sourceLabel = null) => Task.CompletedTask;
    }
}
