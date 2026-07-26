using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Memory;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Contracts.Commands;
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
/// p0380: the run-side twin of state.done — WriteRunResult on a GREEN verdict
/// writes/updates ONE curated `project` memory; a failed run writes nothing.
/// </summary>
public sealed class RunNarrativeMemoryTests
{
    private const string SampleRunId = "2026-05-20T22-27-43-8a3f";
    private const string EntryPath = "/work/.agentsmith/memory/ticket-42.md";
    private const string IndexPath = "/work/.agentsmith/memory/MEMORY.md";

    private readonly InMemorySandboxFileReader _reader = new();
    private readonly WriteRunResultHandler _sut;

    public RunNarrativeMemoryTests()
    {
        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(_reader);
        _sut = new WriteRunResultHandler(
            factory.Object, new InMemoryDialogueTrail(), new InMemoryRunArtifactStore(),
            new Events.RecordingEventPublisher(),
            new RunNarrativeMemoryWriter(NullLogger<RunNarrativeMemoryWriter>.Instance),
            NullLogger<WriteRunResultHandler>.Instance);
    }

    [Fact]
    public async Task GreenRun_AppendsOneCuratedProjectMemory_FailedRun_AppendsNothing()
    {
        // Green pass: no failure reason set, ticket present.
        await _sut.ExecuteAsync(CreateContext(), CancellationToken.None);

        _reader.Files.Should().ContainKey(EntryPath, "a green ticket run MUST leave its narrative");
        _reader.Files[EntryPath].Should().Contain("type: project")
            .And.Contain("Ticket 42: Add login feature")
            .And.Contain(SampleRunId);
        _reader.Files[IndexPath].Split('\n')
            .Count(l => l.StartsWith("- [ticket-42]", StringComparison.Ordinal))
            .Should().Be(1, "ONE curated line per ticket");

        // Failed pass: explicit failure reason => nothing new in the store.
        _reader.Files.Remove(EntryPath);
        _reader.Files.Remove(IndexPath);
        var failed = CreateContext();
        failed.Pipeline.Set(ContextKeys.FailureReason, "verification failed");

        await _sut.ExecuteAsync(failed, CancellationToken.None);

        _reader.Files.Should().NotContainKey(EntryPath, "failed/noisy runs write NOTHING");
        _reader.Files.Should().NotContainKey(IndexPath);
    }

    [Fact]
    public async Task GreenRun_SameTicketAgain_UpdatesEntryInsteadOfAppending()
    {
        await _sut.ExecuteAsync(CreateContext(), CancellationToken.None);
        await _sut.ExecuteAsync(CreateContext(), CancellationToken.None);

        _reader.Files[IndexPath].Split('\n')
            .Count(l => l.StartsWith("- [ticket-42]", StringComparison.Ordinal))
            .Should().Be(1, "dedupe/update by ticket, never append-per-run");
    }

    [Fact]
    public async Task GreenRun_MemoryWriteThrows_RunStillSucceeds()
    {
        var throwing = new Mock<ISandboxFileReader>();
        throwing.Setup(r => r.TryReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        throwing.Setup(r => r.WriteAsync(
                It.Is<string>(p => p.Contains("/memory/")), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("disk gone"));
        throwing.Setup(r => r.WriteAsync(
                It.Is<string>(p => !p.Contains("/memory/")), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(throwing.Object);
        var sut = new WriteRunResultHandler(
            factory.Object, new InMemoryDialogueTrail(), new InMemoryRunArtifactStore(),
            new Events.RecordingEventPublisher(),
            new RunNarrativeMemoryWriter(NullLogger<RunNarrativeMemoryWriter>.Instance),
            NullLogger<WriteRunResultHandler>.Instance);

        var result = await sut.ExecuteAsync(CreateContext(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue("a failed memory write is logged loudly but never fails the run");
    }

    private static WriteRunResultContext CreateContext()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Sandbox, Mock.Of<ISandbox>());
        pipeline.Set(ContextKeys.RunId, SampleRunId);
        var repo = new Repository(new BranchName("feature/test"), "https://github.com/test/test");
        var ticket = new Ticket(new TicketId("42"), "Add login feature", "Description", null, "Open", "github");
        var steps = new List<PlanStep> { new(1, "Create login component", new FilePath("src/Login.cs"), "Create") };
        var plan = new Plan("Test summary", steps, "{}");
        var changes = new List<CodeChange> { new(new FilePath("src/Login.cs"), "public class Login {}", "Create") };
        return new WriteRunResultContext(repo, plan, ticket, changes, pipeline);
    }
}
