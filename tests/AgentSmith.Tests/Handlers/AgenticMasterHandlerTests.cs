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

    private static AgenticMasterHandler Build(IAgenticLoopRunner loop, IPromptCatalog prompts) =>
        new(loop, prompts, new NoOpDecisionLogger(), AgentSmithConfig.Empty(),
            new AgentSmith.Infrastructure.Services.ContextYamlSerializer(),
            dialogueTransport: null, NullLogger<AgenticMasterHandler>.Instance);

    private static AgenticMasterContext BuildContext(
        string masterSkillName,
        bool includeTicket = true,
        string codingPrinciples = "principles")
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
            AgentConfig: new AgentConfig(),
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
