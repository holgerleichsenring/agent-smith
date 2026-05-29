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

    private static AgenticMasterHandler Build(IAgenticLoopRunner loop, IPromptCatalog prompts) =>
        new(loop, prompts, new NoOpDecisionLogger(), dialogueTransport: null,
            NullLogger<AgenticMasterHandler>.Instance);

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

    private sealed class NoOpDecisionLogger : IDecisionLogger
    {
        public Task LogAsync(string? repoPath, DecisionCategory category, string decision,
            CancellationToken cancellationToken = default, string? sourceLabel = null) => Task.CompletedTask;
    }
}
