using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Memory;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Dialogue;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Memory;

/// <summary>
/// 2026-10-12-f5f7c: a green run's narrative is written into every repository it changed, each
/// with its own changes — not once into the first repository with all of them.
/// </summary>
public sealed class RunNarrativePerRepositoryTests
{
    private const string EntryPath = "/work/.agentsmith/memory/ticket-42.md";
    private readonly Dictionary<ISandbox, InMemorySandboxFileReader> _readers = new();

    [Fact]
    public async Task WriteRunResult_TwoChangedRepos_NarrativeInEachWithOwnChanges()
    {
        var (server, client, docs) = (Sandbox(), Sandbox(), Sandbox());
        var context = Context(
            new() { ["server"] = server, ["client"] = client, ["docs"] = docs },
            [new(new FilePath("server/src/Api.cs"), "x", "Modify"),
             new(new FilePath("client/src/App.ts"), "y", "Modify"),
             new(new FilePath("docs/.agentsmith/runs/r/plan.md"), "z", "Create")]);

        await Handler().ExecuteAsync(context, CancellationToken.None);

        _readers[server].Files[EntryPath].Should().Contain("server/src/Api.cs").And.NotContain("client/src/App.ts");
        _readers[client].Files[EntryPath].Should().Contain("client/src/App.ts").And.NotContain("server/src/Api.cs");
        _readers[docs].Files.Should().NotContainKey(EntryPath, "a repository with only its run record did not change");
    }

    private ISandbox Sandbox()
    {
        var sandbox = Mock.Of<ISandbox>();
        _readers[sandbox] = new InMemorySandboxFileReader();
        return sandbox;
    }

    private WriteRunResultHandler Handler()
    {
        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns<ISandbox>(s => _readers[s]);
        return new WriteRunResultHandler(
            factory.Object, new InMemoryDialogueTrail(), new InMemoryRunArtifactStore(),
            new Events.RecordingEventPublisher(),
            new RunNarrativeMemoryWriter(NullLogger<RunNarrativeMemoryWriter>.Instance), AgentSmith.Tests.TestHelpers.MemoryOutcomes.None(),
            new SandboxTargets(), AgentSmith.Tests.TestHelpers.TestRunResultChanges.Create(), NullLogger<WriteRunResultHandler>.Instance);
    }

    private static WriteRunResultContext Context(Dictionary<string, ISandbox> sandboxes, List<CodeChange> changes)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "2026-10-12T10-00-00-f5f7");
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, sandboxes);
        pipeline.Set<IReadOnlyDictionary<string, string>>(
            ContextKeys.SandboxRepos, sandboxes.Keys.ToDictionary(k => k, k => k));
        pipeline.Set<IReadOnlyList<RepoConnection>>(
            ContextKeys.Repos, [.. sandboxes.Keys.Select(k => new RepoConnection { Name = k })]);
        var ticket = new Ticket(new TicketId("42"), "Add login feature", "Description", null, "Open", "github");
        var plan = new Plan("Summary", [new(1, "Do it", new FilePath("src/Api.cs"), "Modify")], "{}");
        return new WriteRunResultContext(
            new Repository(new BranchName("feature/x"), "https://github.com/test/test"), plan, ticket, changes, pipeline);
    }
}
