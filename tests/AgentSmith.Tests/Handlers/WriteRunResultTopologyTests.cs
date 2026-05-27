using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
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

public sealed class WriteRunResultTopologyTests
{
    private const string SampleRunId = "2026-05-20T22-27-43-8a3f";
    private readonly InMemoryDialogueTrail _dialogueTrail = new();
    private readonly Dictionary<string, string> _written = new();
    private readonly WriteRunResultHandler _sut;

    public WriteRunResultTopologyTests()
    {
        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.TryReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((p, _) =>
                _written.TryGetValue(p, out var c) ? Task.FromResult<string?>(c) : Task.FromResult<string?>(null));
        reader.Setup(r => r.WriteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((p, c, _) => _written[p] = c)
            .Returns(Task.CompletedTask);

        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(reader.Object);

        _sut = new WriteRunResultHandler(
            factory.Object, _dialogueTrail, NullLogger<WriteRunResultHandler>.Instance);
    }

    [Fact]
    public async Task WriteRunResult_SingleRepo_PersistsRepoModeMono()
    {
        await ExecuteWithRepos([new RepoConnection { Name = "primary" }], CancellationToken.None);

        var content = ResultContent();
        content.Should().Contain("repo_mode: mono");
        content.Should().Contain("repos:");
        content.Should().Contain("- primary");
    }

    [Fact]
    public async Task WriteRunResult_ThreeRepos_PersistsRepoModeMultiAndRepoNames()
    {
        var repos = new[]
        {
            new RepoConnection { Name = "api" },
            new RepoConnection { Name = "worker" },
            new RepoConnection { Name = "web" },
        };

        await ExecuteWithRepos(repos, CancellationToken.None);

        var content = ResultContent();
        content.Should().Contain("repo_mode: multi");
        content.Should().Contain("- api");
        content.Should().Contain("- worker");
        content.Should().Contain("- web");
    }

    [Fact]
    public async Task WriteRunResult_FiveSandboxes_PersistsSandboxCountFive()
    {
        var pipeline = NewPipelineWithSandbox();
        var sandboxes = new Dictionary<string, ISandbox>
        {
            ["api"] = Mock.Of<ISandbox>(),
            ["worker"] = Mock.Of<ISandbox>(),
            ["web"] = Mock.Of<ISandbox>(),
            ["docs"] = Mock.Of<ISandbox>(),
            ["ops"] = Mock.Of<ISandbox>(),
        };
        pipeline.Set(ContextKeys.Sandboxes, (IReadOnlyDictionary<string, ISandbox>)sandboxes);

        await _sut.ExecuteAsync(CreateContext("Feature", pipeline), CancellationToken.None);

        ResultContent().Should().Contain("sandbox_count: 5");
    }

    [Fact]
    public async Task WriteRunResult_FrontmatterIncludesPipelineNameAndStatus()
    {
        var pipeline = NewPipelineWithSandbox();
        pipeline.Set(ContextKeys.PipelineName, "fix-bug");

        await _sut.ExecuteAsync(CreateContext("Fix something", pipeline), CancellationToken.None);

        var content = ResultContent();
        content.Should().Contain("pipeline_name: fix-bug");
        content.Should().Contain("status: done");
        content.Should().Contain("run_id: " + SampleRunId);
    }

    private async Task ExecuteWithRepos(IReadOnlyList<RepoConnection> repos, CancellationToken ct)
    {
        var pipeline = NewPipelineWithSandbox();
        pipeline.Set(ContextKeys.Repos, repos);
        await _sut.ExecuteAsync(CreateContext("Add feature", pipeline), ct);
    }

    private string ResultContent()
    {
        var entry = _written.FirstOrDefault(kv => kv.Key.EndsWith("result.md"));
        entry.Key.Should().NotBeNull();
        return entry.Value;
    }

    private PipelineContext NewPipelineWithSandbox()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Sandbox, Mock.Of<ISandbox>());
        pipeline.Set(ContextKeys.RunId, SampleRunId);
        return pipeline;
    }

    private static WriteRunResultContext CreateContext(string ticketTitle, PipelineContext pipeline)
    {
        var repo = new Repository(new BranchName("feature/test"), "https://github.com/test/test");
        var ticket = new Ticket(new TicketId("42"), ticketTitle, "Description", null, "Open", "github");
        var plan = new Plan("Summary", new List<PlanStep>
        {
            new(1, "Step", new FilePath("src/x.cs"), "Create"),
        }, "{}");
        var changes = new List<CodeChange> { new(new FilePath("src/x.cs"), "code", "Create") };
        return new WriteRunResultContext(repo, plan, ticket, changes, pipeline);
    }
}
