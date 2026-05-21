using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Dialogue;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

public sealed class WriteRunResultHandlerArtifactsTests
{
    private readonly Dictionary<string, string> _written = new();
    private readonly Dictionary<string, string?> _initial = new();
    private readonly WriteRunResultHandler _sut;

    public WriteRunResultHandlerArtifactsTests()
    {
        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.TryReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((p, _) =>
            {
                if (_written.TryGetValue(p, out var c)) return Task.FromResult<string?>(c);
                if (_initial.TryGetValue(p, out var i)) return Task.FromResult(i);
                return Task.FromResult<string?>(null);
            });
        reader.Setup(r => r.WriteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((p, c, _) => _written[p] = c)
            .Returns(Task.CompletedTask);

        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(reader.Object);

        _initial["/work/.agentsmith/context.yaml"] = "state:\n  done: {}\n  active: {}";

        _sut = new WriteRunResultHandler(
            factory.Object, new InMemoryDialogueTrail(), NullLogger<WriteRunResultHandler>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_PlanJsonInContext_WritesPlanJsonToRunDir()
    {
        var pipeline = NewPipelineWithSandbox();
        pipeline.Set(ContextKeys.PlanJson, "{\"summary\":\"x\"}");

        await _sut.ExecuteAsync(NewContext(pipeline), CancellationToken.None);

        var planJson = _written.Single(kv => kv.Key.EndsWith("plan.json"));
        planJson.Value.Should().Contain("\"summary\": \"x\"");
    }

    [Fact]
    public async Task ExecuteAsync_DiffJsonInContext_WritesDiffJsonToRunDir()
    {
        var pipeline = NewPipelineWithSandbox();
        pipeline.Set(ContextKeys.DiffJson, "{\"changes\":[]}");

        await _sut.ExecuteAsync(NewContext(pipeline), CancellationToken.None);

        var diffJson = _written.Single(kv => kv.Key.EndsWith("diff.json"));
        diffJson.Value.Should().Contain("\"changes\"");
    }

    [Fact]
    public async Task ExecuteAsync_BootstrapMarkdownInContext_WritesBootstrapMdToRunDir()
    {
        var pipeline = NewPipelineWithSandbox();
        pipeline.Set(ContextKeys.BootstrapMarkdown, "# Bootstrap\n\nDetected stack: .NET 8");

        await _sut.ExecuteAsync(NewContext(pipeline), CancellationToken.None);

        var bootstrap = _written.Single(kv => kv.Key.EndsWith("bootstrap.md"));
        bootstrap.Value.Should().Contain("# Bootstrap");
    }

    [Fact]
    public async Task ExecuteAsync_NoArtifactsInContext_OnlyWritesPlanAndResult()
    {
        var pipeline = NewPipelineWithSandbox();

        await _sut.ExecuteAsync(NewContext(pipeline), CancellationToken.None);

        _written.Should().NotContainKey(_written.Keys.FirstOrDefault(k => k.EndsWith("plan.json")) ?? "missing");
        _written.Keys.Should().NotContain(k => k.EndsWith("diff.json") || k.EndsWith("bootstrap.md"));
    }

    [Fact]
    public async Task ExecuteAsync_PerSkillBreakdownPresent_RendersBreakdownSection()
    {
        var pipeline = NewPipelineWithSandbox();
        var tracker = PipelineCostTracker.GetOrCreate(pipeline);
        var scope = tracker.BeginCall("plan-author", "planner", SkillExecutionPhase.Plan);
        scope.Finalize(new LimitEnforcer(new Contracts.Models.Configuration.LoopLimitsConfig(),
            new CancellationTokenSource()));
        scope.Dispose();

        await _sut.ExecuteAsync(NewContext(pipeline), CancellationToken.None);

        var result = _written.Single(kv => kv.Key.EndsWith("result.md"));
        result.Value.Should().Contain("## Per-skill cost breakdown");
        result.Value.Should().Contain("plan-author (planner, Plan)");
    }

    [Fact]
    public async Task ExecuteAsync_PerSkillBreakdownEmpty_NoBreakdownSection()
    {
        var pipeline = NewPipelineWithSandbox();

        await _sut.ExecuteAsync(NewContext(pipeline), CancellationToken.None);

        var result = _written.Single(kv => kv.Key.EndsWith("result.md"));
        result.Value.Should().NotContain("Per-skill cost breakdown");
    }

    private static PipelineContext NewPipelineWithSandbox()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Sandbox, Mock.Of<ISandbox>());
        pipeline.Set(ContextKeys.RunId, "2026-05-20T22-27-43-8a3f");
        return pipeline;
    }

    private static WriteRunResultContext NewContext(PipelineContext pipeline)
    {
        var repo = new Repository(new BranchName("feature/x"), "https://example.test/x");
        var ticket = new Ticket(new TicketId("99"), "Add caching layer", "desc", null, "Open", "github");
        var plan = new Plan("Summary", Array.Empty<PlanStep>(), "{}");
        return new WriteRunResultContext(repo, plan, ticket, Array.Empty<CodeChange>(), pipeline);
    }
}
