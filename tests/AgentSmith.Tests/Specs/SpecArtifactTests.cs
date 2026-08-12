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
using AgentSmith.Sandbox.Wire;
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
    public async Task SpecSetWriter_NewRevision_RemovesFilesAbsentFromCut()
    {
        var files = new RecordingFileReader();
        files.Existing.AddRange(
        [
            ".agentsmith/specs/azdo-19106/set.yaml",
            ".agentsmith/specs/azdo-19106/accounting.md",
            ".agentsmith/specs/azdo-19106/p19106-whole-ticket.yaml",
            ".agentsmith/specs/azdo-19106/p19106-whole-ticket.md",
        ]);
        var steps = new List<Step>();
        var pipeline = PipelineWithSandbox(RecordingSandbox(steps));

        await Writer(files).WriteAsync(pipeline, new RepoConnection { Name = "primary" }, TwoPhaseSet(), default);

        var removed = GitRemoveArgs(steps);
        removed.Should().Contain(".agentsmith/specs/azdo-19106/p19106-whole-ticket.yaml");
        removed.Should().Contain(".agentsmith/specs/azdo-19106/p19106-whole-ticket.md");
        removed.Should().NotContain(a => a.EndsWith("set.yaml") || a.EndsWith("accounting.md"));
    }

    [Fact]
    public async Task SpecSetWriter_IndexAndAccounting_SurviveReplace()
    {
        var files = new RecordingFileReader();
        files.Existing.AddRange(
        [
            ".agentsmith/specs/azdo-19106/set.yaml",
            ".agentsmith/specs/azdo-19106/accounting.md",
            ".agentsmith/specs/azdo-19106/p19106a-first.yaml",
            ".agentsmith/specs/azdo-19106/p19106a-first.md",
        ]);
        var steps = new List<Step>();
        var pipeline = PipelineWithSandbox(RecordingSandbox(steps));

        await Writer(files).WriteAsync(pipeline, new RepoConnection { Name = "primary" }, TwoPhaseSet(), default);

        // The directory held nothing but the (partial) current cut plus index and
        // accounting — a revision that is a full replace still deletes NOTHING here.
        GitRemoveArgs(steps).Should().BeEmpty();
    }

    [Fact]
    public void SpecSetIndex_RoundTripsTheOrderTheAccountingAndTheExecutedHead()
    {
        var set = TwoPhaseSet() with { Executed = ["p19106a"] };

        var index = new SpecSetIndex();
        var doc = index.Parse(index.Serialize(set))!;

        doc.Phases.Should().Equal("p19106a-first", "p19106b-second");
        doc.ExecutedPhases.Should().Equal("p19106a");
        doc.Discarded.Should().ContainSingle().Which.Reason.Should().Be("a sign-off");
        index.AccountingOf(doc).Carried.Should().HaveCount(2);
        index.RevisionsOf(doc)[^1].Cause.Should().Be(SpecRevisionCause.Initial);
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
            new SpecSetIndex(),
            NullLogger<SpecSetWriter>.Instance);
    }

    // p0399: exit 0 on every step keeps the writer on the "unchanged" path after the
    // deletes, so the tests observe the staged replace without mocking commit + push.
    private static ISandbox RecordingSandbox(List<Step> steps)
    {
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Callback<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) => steps.Add(step))
            .ReturnsAsync(new StepResult(1, Guid.Empty, 0, false, 0, null, null));
        return sandbox.Object;
    }

    private static IReadOnlyList<string> GitRemoveArgs(IEnumerable<Step> steps) =>
        [.. steps
            .Where(s => s.Command == "git" && s.Args is ["rm", ..])
            .SelectMany(s => s.Args!)];

    private static PipelineContext PipelineWithSandbox(ISandbox? sandbox = null)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox> { ["primary"] = sandbox ?? Mock.Of<ISandbox>() });
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

        /// <summary>Files already on the branch from the previous revision.</summary>
        public List<string> Existing { get; } = [];

        public Task<bool> ExistsAsync(string path, CancellationToken ct) => Task.FromResult(false);
        public Task<string?> TryReadAsync(string path, CancellationToken ct) =>
            Task.FromResult<string?>(null);
        public Task<string> ReadRequiredAsync(string path, CancellationToken ct) =>
            Task.FromResult(string.Empty);

        public Task<IReadOnlyList<string>> ListAsync(string path, int? maxDepth, CancellationToken ct)
        {
            var prefix = path.TrimEnd('/') + "/";
            IReadOnlyList<string> listed = [.. Existing.Concat(Written.Keys)
                .Where(p => p.StartsWith(prefix, StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)];
            return Task.FromResult(listed);
        }

        public Task WriteAsync(string path, string content, CancellationToken ct)
        {
            Written[path] = content;
            return Task.CompletedTask;
        }
    }
}
