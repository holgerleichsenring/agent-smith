using AgentSmith.Application.Services;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0393a: the spec set rides the ticket branch — yaml for what and done, markdown for
/// the verbatim spans, and an index the next run reads back. Git is the UI: diff, blame,
/// history and the pull-request review need no new surface.
/// </summary>
public sealed class SpecArtifactTests
{
    [Fact]
    public async Task SpecArtifacts_AreWrittenToTheTicketBranchUnderSpecsProviderId()
    {
        var files = new RecordingFileReader();
        var pipeline = PipelineWithSandbox();
        var set = TwoPhaseSet();

        await Writer(files).WriteAsync(pipeline, new RepoConnection { Name = "primary" }, set, default);

        files.Written.Keys.Should().Contain(
        [
            ".agentsmith/specs/azdo-19106/set.yaml",
            ".agentsmith/specs/azdo-19106/p19106a-first.yaml",
            ".agentsmith/specs/azdo-19106/p19106a-first.md",
            ".agentsmith/specs/azdo-19106/p19106b-second.yaml",
            ".agentsmith/specs/azdo-19106/p19106b-second.md",
            ".agentsmith/specs/azdo-19106/accounting.md",
        ]);
        files.Written[".agentsmith/specs/azdo-19106/p19106a-first.md"]
            .Should().Contain("verbatim from segment one");
    }

    [Fact]
    public void SpecSetIndex_RoundTripsTheOrderTheAccountingAndTheExecutedHead()
    {
        var set = TwoPhaseSet() with { Executed = ["p19106a"] };

        var doc = SpecSetIndex.Parse(SpecSetIndex.Serialize(set))!;

        doc.Phases.Should().Equal("p19106a-first", "p19106b-second");
        doc.ExecutedPhases.Should().Equal("p19106a");
        doc.Discarded.Should().ContainSingle().Which.Reason.Should().Be("a sign-off");
        SpecSetIndex.AccountingOf(doc).Carried.Should().HaveCount(2);
        SpecSetIndex.RevisionsOf(doc)[^1].Cause.Should().Be(SpecRevisionCause.Initial);
    }

    [Fact]
    public void SpecSetKey_IsProviderAndTicketId_SoMergedSpecsCoexistInTheTrunk()
    {
        var key = SpecSetKey.For("AzureDevOps", "AB#19106");

        key.Value.Should().Be("azuredevops-ab-19106");
        key.Directory.Should().Be(".agentsmith/specs/azuredevops-ab-19106");
        key.YamlPath("p1a-x").Should().EndWith("/p1a-x.yaml");
    }

    private static SpecSetWriter Writer(ISandboxFileReader files)
    {
        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(files);
        return new SpecSetWriter(
            factory.Object,
            new SandboxGitOperations(
                NullLogger<SandboxGitOperations>.Instance, factory.Object),
            NullLogger<SpecSetWriter>.Instance);
    }

    private static PipelineContext PipelineWithSandbox()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox> { ["primary"] = Mock.Of<ISandbox>() });
        pipeline.Set(
            ContextKeys.Repository,
            new Repository(new BranchName("agent-smith/19106"), "https://example.test/repo.git"));
        pipeline.Set<IReadOnlyList<TicketSegment>>(
            ContextKeys.TicketSegments, [new TicketSegment(1, "verbatim from segment one", 1, 1)]);
        return pipeline;
    }

    private static SpecSet TwoPhaseSet() => new(
        "azdo-19106",
        [Phase("p19106a", "first"), Phase("p19106b", "second")],
        new SpecAccounting(
            [new CarriedSegment(1, "p19106a"), new CarriedSegment(2, "p19106b")],
            [new DiscardedSegment(3, "a sign-off")],
            []),
        [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow)],
        SpecSource.Derived);

    private static SpecPhase Phase(string id, string slug) => new(
        new PhaseDraft(id, $"Goal {id}", $"phase: {id}\ngoal: \"Goal {id}\"", []),
        slug,
        $"# {id}\n\nverbatim from segment one\n",
        [1]);

    private sealed class RecordingFileReader : ISandboxFileReader
    {
        public Dictionary<string, string> Written { get; } = new(StringComparer.Ordinal);

        public Task<bool> ExistsAsync(string path, CancellationToken ct) => Task.FromResult(false);
        public Task<string?> TryReadAsync(string path, CancellationToken ct) =>
            Task.FromResult<string?>(null);
        public Task<string> ReadRequiredAsync(string path, CancellationToken ct) =>
            Task.FromResult(string.Empty);
        public Task<IReadOnlyList<string>> ListAsync(string path, int? maxDepth, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task WriteAsync(string path, string content, CancellationToken ct)
        {
            Written[path] = content;
            return Task.CompletedTask;
        }
    }
}
