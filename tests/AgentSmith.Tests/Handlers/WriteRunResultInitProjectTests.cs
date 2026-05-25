using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Dialogue;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// p0161d: init-project runs fan out per RepoConnection. plan.md derives
/// from DiscoveredComponents; result.md reaches parity with fix-bug
/// (cost + duration + trail + decisions + per-component bootstrap output).
/// </summary>
public sealed class WriteRunResultInitProjectTests
{
    private const string SampleRunId = "2026-05-25T22-27-43-8a3f";

    private readonly InMemoryDialogueTrail _dialogueTrail = new();
    // (sandboxId, path) → content. Per-sandbox prefixes make multi-repo writes
    // distinguishable even when they share a relative path under /work.
    private readonly Dictionary<string, string> _written = new();
    private readonly Dictionary<ISandbox, string> _sandboxIds = new();
    private readonly WriteRunResultHandler _sut;

    public WriteRunResultInitProjectTests()
    {
        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>()))
            .Returns<ISandbox>(sandbox =>
            {
                if (!_sandboxIds.TryGetValue(sandbox, out var id))
                {
                    id = $"sb{_sandboxIds.Count}";
                    _sandboxIds[sandbox] = id;
                }
                var reader = new Mock<ISandboxFileReader>();
                reader.Setup(r => r.TryReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Returns<string, CancellationToken>((p, _) =>
                        _written.TryGetValue($"{id}::{p}", out var c)
                            ? Task.FromResult<string?>(c) : Task.FromResult<string?>(null));
                reader.Setup(r => r.WriteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Callback<string, string, CancellationToken>((p, c, _) => _written[$"{id}::{p}"] = c)
                    .Returns(Task.CompletedTask);
                return reader.Object;
            });
        _sut = new WriteRunResultHandler(
            factory.Object, _dialogueTrail, NullLogger<WriteRunResultHandler>.Instance);
    }

    [Fact]
    public async Task WriteRunResult_InitProject_PlanMdContainsDiscoveryTable()
    {
        var pipeline = NewInitPipeline(repos: ["monorepo"], components: new()
        {
            ["monorepo"] = new[]
            {
                new DiscoveredComponent("server", ".", "csharp", "src/server/Program.cs"),
                new DiscoveredComponent("client", "client", "typescript", "client/package.json"),
            },
        });
        var context = NewInitContext(pipeline);

        await _sut.ExecuteAsync(context, CancellationToken.None);

        var plan = _written.First(kv => kv.Key.EndsWith("plan.md")).Value;
        plan.Should().Contain("Discovered components");
        plan.Should().Contain("| server | `.` | csharp | `src/server/Program.cs` |");
        plan.Should().Contain("| client | `client` | typescript | `client/package.json` |");
    }

    [Fact]
    public async Task WriteRunResult_InitProject_FansOutPerRepo()
    {
        // Each RepoConnection gets its own runs/{runId}-init/plan.md +
        // result.md. Same RunId, distinct sandbox writes via SandboxesForRepo.
        var pipeline = NewInitPipeline(
            repos: ["server", "client"],
            components: new()
            {
                ["server"] = new[] { new DiscoveredComponent("default", ".", "csharp", "Program.cs") },
                ["client"] = new[] { new DiscoveredComponent("default", ".", "typescript", "package.json") },
            });
        var context = NewInitContext(pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // 2 plan.md + 2 result.md
        _written.Keys.Count(k => k.EndsWith("plan.md")).Should().Be(2);
        _written.Keys.Count(k => k.EndsWith("result.md")).Should().Be(2);
        // Both plan.md files carry their own repo's section heading.
        var planContents = _written.Where(kv => kv.Key.EndsWith("plan.md")).Select(kv => kv.Value).ToList();
        planContents.Should().Contain(c => c.Contains("repo `server`"));
        planContents.Should().Contain(c => c.Contains("repo `client`"));
    }

    [Fact]
    public async Task WriteRunResult_InitProject_ResultMdHasParityWithFixBug()
    {
        // Init-mode result.md should carry the same artefact shape as a
        // fix-bug result.md: yaml frontmatter (date/type/result + cost +
        // duration), discovery table, bootstrap-output sections,
        // ExecutionTrail. We check the frontmatter + discovery section here.
        var pipeline = NewInitPipeline(repos: ["mono"], components: new()
        {
            ["mono"] = new[] { new DiscoveredComponent("default", ".", "csharp", "Program.cs") },
        });
        pipeline.Set(ContextKeys.RunDurationSeconds, 42);
        pipeline.Set(ContextKeys.ExecutionTrail, new List<ExecutionTrailEntry>
        {
            new("CheckoutSourceCommand", null, true, "OK", DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2), null),
            new("BootstrapDiscoverCommand", "project-discovery", true, "1 component", DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5), null),
        });
        pipeline.Set(ContextKeys.BootstrapOutputs,
            new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal)
            {
                ["mono"] = new(StringComparer.Ordinal) { ["default"] = "Wrote context.yaml + coding-principles.md." },
            });
        var context = NewInitContext(pipeline);

        await _sut.ExecuteAsync(context, CancellationToken.None);

        var resultMd = _written.First(kv => kv.Key.EndsWith("result.md")).Value;
        resultMd.Should().Contain("type: init");
        resultMd.Should().Contain("duration_seconds: 42");
        resultMd.Should().Contain("Discovered components");
        resultMd.Should().Contain("Bootstrap output per component");
        resultMd.Should().Contain("Wrote context.yaml + coding-principles.md.");
        resultMd.Should().Contain("Execution Trail");
        resultMd.Should().Contain("BootstrapDiscoverCommand");
    }

    [Fact]
    public async Task WriteRunResult_InitProject_CostAggregatedAndPerRepoAttributed()
    {
        // For multi-repo init the per-repo result.md should carry the shared
        // cost note (Discover is per-repo; BootstrapRound calls are tagged
        // per repo by PipelineCostTracker — p0158g).
        var pipeline = NewInitPipeline(
            repos: ["server", "client"],
            components: new()
            {
                ["server"] = new[] { new DiscoveredComponent("default", ".", "csharp", "Program.cs") },
                ["client"] = new[] { new DiscoveredComponent("default", ".", "typescript", "package.json") },
            });
        pipeline.Set(ContextKeys.RunCostSummary, new RunCostSummary(
            new Dictionary<string, PhaseCost>
            {
                ["scout"] = new("haiku", 1000, 200, 0, 1, 0.01m),
            }.AsReadOnly(),
            0.01m));
        var context = NewInitContext(pipeline);

        await _sut.ExecuteAsync(context, CancellationToken.None);

        var resultMds = _written.Where(kv => kv.Key.EndsWith("result.md")).Select(kv => kv.Value).ToList();
        resultMds.Should().HaveCount(2);
        resultMds.Should().AllSatisfy(md =>
            md.Should().Contain("shared across 2 repos"));
    }

    private static PipelineContext NewInitPipeline(
        string[] repos, Dictionary<string, DiscoveredComponent[]> components)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, SampleRunId);
        pipeline.Set(ContextKeys.ResolvedPipeline, new ResolvedPipelineConfig(
            "init-project", new AgentConfig(), "skills/coding", null));
        pipeline.Set<IReadOnlyList<RepoConnection>>(
            ContextKeys.Repos, repos.Select(r => new RepoConnection { Name = r, Url = "https://x/y.git", Auth = "test" }).ToList());
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            repos.ToDictionary(r => r, _ => Mock.Of<ISandbox>(), StringComparer.Ordinal));
        pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<DiscoveredComponent>>>(
            ContextKeys.DiscoveredComponents,
            components.ToDictionary(
                kv => kv.Key,
                kv => (IReadOnlyList<DiscoveredComponent>)kv.Value,
                StringComparer.Ordinal));
        return pipeline;
    }

    private static WriteRunResultContext NewInitContext(PipelineContext pipeline)
    {
        var repo = new Repository(new BranchName("agentsmith/init"), "https://x/y.git");
        // p0161d: init-mode runs have null Plan + null Ticket (the discriminator).
        return new WriteRunResultContext(repo, Plan: null, Ticket: null, Changes: [], pipeline);
    }
}
