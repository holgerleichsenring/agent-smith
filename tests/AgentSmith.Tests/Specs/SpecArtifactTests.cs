using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
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
    private const string Base = "2026-10-06-1a1a";
    private const string A = Base + "a";
    private const string B = Base + "b";

    // 2026-10-06-03c7d: one manifest under series/, the specs and companions in specs/planned/,
    // and nothing under a per-ticket directory.
    [Fact]
    public async Task SeriesWriter_DerivedRun_WritesManifestAndPlannedSpecs()
    {
        var files = new RecordingFileReader();

        await Writer(files).WriteAsync(PipelineWithSandbox(), new RepoConnection { Name = "primary" }, TwoPhaseSet(), default);

        files.Written.Keys.Should().BeEquivalentTo(
        [
            $".agentsmith/series/{Base}.yaml",
            $".agentsmith/specs/planned/{A}-first.yaml",
            $".agentsmith/specs/planned/{A}-first.md",
            $".agentsmith/specs/planned/{B}-second.yaml",
            $".agentsmith/specs/planned/{B}-second.md",
        ]);
        files.Written[$".agentsmith/specs/planned/{A}-first.md"].Should().Contain("verbatim from segment one");
        files.Written.Keys.Should().NotContain(k => k.Contains("azdo-19106"), "nothing lies under a ticket directory");
    }

    [Fact]
    public async Task SeriesWriter_DroppedSpecOfAnotherSeries_IsNeverRemoved()
    {
        var files = new RecordingFileReader();
        files.Existing.AddRange(
        [
            $".agentsmith/specs/planned/{Base}c-dropped.yaml",
            ".agentsmith/specs/planned/2026-10-06-9f9fa-other-series.yaml",
            $".agentsmith/specs/planned/{A}.html",
        ]);
        var steps = new List<Step>();

        await Writer(files).WriteAsync(
            PipelineWithSandbox(RecordingSandbox(steps)), new RepoConnection { Name = "primary" }, TwoPhaseSet(), default);

        GitRemoveArgs(steps).Should().Contain($".agentsmith/specs/planned/{Base}c-dropped.yaml")
            .And.NotContain(a => a.Contains("other-series"), "another series' spec is not this cut's to remove")
            .And.NotContain(a => a.EndsWith(".html"), "a kept spec's mock is a companion the renderer never writes");
    }

    [Fact]
    public async Task SeriesWriter_ForceStagesOnlyWhatItWrote()
    {
        var files = new RecordingFileReader();
        var steps = new List<Step>();

        await Writer(files).WriteAsync(
            PipelineWithSandbox(RecordingSandbox(steps)), new RepoConnection { Name = "primary" }, TwoPhaseSet(), default);

        var add = steps.Single(s => s.Command == "git" && s.Args is ["add", "-f", ..]).Args!;
        add.Should().Contain(files.Written.Keys).And.NotContain(".agentsmith/specs/planned");
    }

    [Fact]
    public void SeriesManifest_RoundTripsTheOrderTheAccountingAndTheExecutedHead()
    {
        var set = TwoPhaseSet() with { Executed = [A] };

        var manifest = new SeriesManifest();
        var doc = manifest.Parse(manifest.Serialize(set))!;

        doc.Ticket.Should().Be("azdo-19106");
        doc.Specs.Should().Equal(A, B);
        doc.ExecutedSpecs.Should().Equal(A);
        doc.Discarded.Should().ContainSingle().Which.Reason.Should().Be("a sign-off");
        manifest.AccountingOf(doc).Carried.Should().HaveCount(2);
        manifest.RevisionsOf(doc)[^1].Cause.Should().Be(SpecRevisionCause.Initial);
    }

    // 2026-09-07-c9d4: the readings and the taken index survive the branch, so the next run
    // names the reading it proceeds on from the question that was actually asked.
    [Fact]
    public void SeriesManifest_RoundTripsAQuestionHandbackWithItsReadings()
    {
        var question = new SpecHandback(
            SpecHandbackCase.Question, "reads two ways",
            Readings: ["only where an advisory forces it", "everywhere"], Taken: 1);
        var set = TwoPhaseSet() with { Phases = [], Handback = question };

        var manifest = new SeriesManifest();
        var read = manifest.HandbackOf(manifest.Parse(manifest.Serialize(set))!)!;

        read.Case.Should().Be(SpecHandbackCase.Question);
        read.Readings.Should().Equal("only where an advisory forces it", "everywhere");
        read.Taken.Should().Be(1);
        read.TakenReading.Should().Be("everywhere");
    }

    [Fact]
    public void SeriesManifest_RoundTrip_KeepsFingerprintPinnedAndDiscarded()
    {
        var accounting = TwoPhaseSet().Accounting with
        {
            DiscardedContexts = [new DiscardedContext("backend", "the audit flags nothing there")],
        };
        var set = TwoPhaseSet() with
        {
            Accounting = accounting, TicketPinnedWhole = true, TicketFingerprint = "fp-1", Goal = "Ship the widget",
        };

        var manifest = new SeriesManifest();
        var doc = manifest.Parse(manifest.Serialize(set))!;

        manifest.AccountingOf(doc).DiscardedContexts.Should().ContainSingle()
            .Which.Should().Be(new DiscardedContext("backend", "the audit flags nothing there"));
        manifest.AccountingOf(doc).Discarded.Should().ContainSingle("the segment accounting is untouched");
        manifest.FingerprintOf(doc).Should().Be("fp-1");
        doc.TicketPinnedWhole.Should().BeTrue();
        doc.Goal.Should().Be("Ship the widget");
    }

    [Fact]
    public void SeriesPaths_BelongTo_MatchesTheIdAndNeverALongerOne()
    {
        SeriesPaths.Manifest(Base).Should().Be($".agentsmith/series/{Base}.yaml");
        SeriesPaths.BelongsTo($"{A}-first.yaml", A).Should().BeTrue();
        SeriesPaths.BelongsTo($"{A}.html", A).Should().BeTrue();
        SeriesPaths.BelongsTo($"{A}b-x.yaml", A).Should().BeFalse();
        SeriesPaths.BelongsToSeries($"{B}-second.md", Base).Should().BeTrue();
        SeriesPaths.BelongsToSeries($"{Base}-x.yaml", Base).Should().BeFalse("the base is not itself a spec");
    }

    private static SeriesWriter Writer(ISandboxFileReader files)
    {
        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(files);
        return AgentSmith.Tests.TestSupport.SeriesDoubles.Writer(factory.Object);
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
        [Phase(A, "first"), Phase(B, "second")],
        new SpecAccounting(
            [new CarriedSegment(1, A), new CarriedSegment(2, B)],
            [new DiscardedSegment(3, "a sign-off")],
            []),
        [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow)],
        SpecSource.Derived,
        Series: Base);

    private static SpecPhase Phase(string id, string slug) => new(
        new PhaseDraft(id, $"Goal {id}", $"spec: {id}\ngoal: \"Goal {id}\"", []),
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
