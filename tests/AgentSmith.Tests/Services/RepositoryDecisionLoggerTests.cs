using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0380 wrote decisions as per-phase / per-run YAML under .agentsmith/decisions/ —
/// decision.schema.json, the IDE format — and those cases are kept here.
/// <para>
/// 2026-09-19-c511a: through the REPOSITORY's file surface. The predecessor took a PATH and gave it
/// to System.IO; the path was always the sandbox mount /work, so in a container every write went to
/// the server process's own root and threw. These tests therefore assert the SEAM — what arrived at
/// the sandbox surface — and never a host directory, because a host directory of the test's own
/// choosing is exactly the shape that hid the bug for a year.
/// </para>
/// <para>2026-09-19-c511b: and what happens when that surface refuses.</para>
/// </summary>
public sealed class RepositoryDecisionLoggerTests
{
    private const string SampleRunId = "2026-05-20T22-27-43-8a3f";
    private const string DecisionsDir = ".agentsmith/decisions";
    private static string RunFile => $"{DecisionsDir}/{SampleRunId}.yaml";

    private readonly AsyncLocalRunContextAccessor _runContext = new();
    private readonly RecordingEventPublisher _events = new();
    private readonly InMemorySandboxFileReader _repository = new();
    private readonly RepositoryDecisionLogger _sut;

    public RepositoryDecisionLoggerTests()
    {
        _sut = Logger(_events);
    }

    private RepositoryDecisionLogger Logger(IEventPublisher publisher) =>
        new(_runContext, new DecisionEventMirror(publisher, _runContext),
            NullLogger<RepositoryDecisionLogger>.Instance);

    [Fact]
    public async Task DecisionLog_WithARepositorySurface_WritesTheYamlThroughTheSandboxNotTheHostFilesystem()
    {
        using var scope = _runContext.BeginScope(SampleRunId);

        var outcome = await _sut.LogAsync(_repository, DecisionCategory.Architecture,
            "Redis Streams: fan-out to multiple consumers required");

        outcome.Should().Be(DecisionLogOutcome.Recorded);
        _repository.Files.Keys.Should().ContainSingle().Which.Should().Be(RunFile,
            "the decision is written where the repository is — inside the run's sandbox");
        Directory.Exists("/work").Should().BeFalse(
            "nothing may create the sandbox mount on the machine running this process");
    }

    [Fact]
    public async Task DecisionLog_RunScoped_WritesDecisionsRunIdYamlAtTheRepositoryRoot()
    {
        using var scope = _runContext.BeginScope(SampleRunId);

        await _sut.LogAsync(_repository, DecisionCategory.Architecture, "run-scoped choice");

        _repository.Files[RunFile].Should().StartWith($"run: {SampleRunId}")
            .And.Contain("decisions:").And.Contain("- category: Architecture");
        _repository.Files.Should().NotContainKey($"{DecisionsDir}/decisions.md",
            "the legacy p0100 append log is retired");
    }

    [Fact]
    public async Task DecisionLog_PhaseLabelled_WritesDecisionsPhaseYaml()
    {
        using var scope = _runContext.BeginScope(SampleRunId);

        await _sut.LogAsync(_repository, DecisionCategory.Implementation, "run-scoped choice");
        await _sut.LogAsync(_repository, DecisionCategory.Tooling, "phase-scoped choice",
            sourceLabel: "p0380");

        _repository.Files[RunFile].Should().StartWith($"run: {SampleRunId}");
        _repository.Files[$"{DecisionsDir}/p0380.yaml"].Should().StartWith("phase: p0380");
    }

    [Fact]
    public async Task DecisionLog_SecondDecisionOnOneLabel_AppendsToTheFileItReadBack()
    {
        using var scope = _runContext.BeginScope(SampleRunId);

        await _sut.LogAsync(_repository, DecisionCategory.Architecture, "first");
        await _sut.LogAsync(_repository, DecisionCategory.TradeOff, "second");

        var yaml = _repository.Files[RunFile];
        yaml.Split("run:").Length.Should().Be(2, "one file per run, decisions array inside");
        yaml.Should().Contain("- category: Architecture").And.Contain("- category: TradeOff");
    }

    [Fact]
    public async Task DecisionLog_FileNotListed_StartsItFromTheHeader()
    {
        using var scope = _runContext.BeginScope(SampleRunId);

        await _sut.LogAsync(_repository, DecisionCategory.Architecture, "first");

        _repository.Files[RunFile].Should().StartWith($"run: {SampleRunId}\ndecisions:\n");
    }

    [Fact]
    public async Task DecisionLog_FileListedButUnreadable_LeavesItAloneAndSaysSo()
    {
        using var scope = _runContext.BeginScope(SampleRunId);
        var repository = new UnreadableFile(RunFile);

        var outcome = await _sut.LogAsync(repository, DecisionCategory.Architecture, "would be lost");

        outcome.Should().Be(DecisionLogOutcome.RecordedWithoutRepositoryCopy);
        repository.Written.Should().BeEmpty(
            "a file that exists and cannot be read is not replaced by a fresh one — the surface "
            + "answers null for a read failure exactly as it does for an absent file");
        _events.Events.Should().ContainSingle("the decision itself is still on the run");
    }

    /// <summary>
    /// The agent lists a relative directory ROOTED at /work (FileStepHandler.Resolve), so what comes
    /// back is "/work/.agentsmith/decisions/&lt;runId&gt;.yaml" — not the relative key the in-memory
    /// double returns. If the lookup did not match that shape, every append would start from the
    /// header and erase the file it meant to extend.
    /// </summary>
    [Fact]
    public async Task DecisionLog_ListingInTheSandboxsAbsoluteShape_IsRecognisedAsTheSameFile()
    {
        using var scope = _runContext.BeginScope(SampleRunId);
        var repository = new ListingOnly(
            [$"/work/{DecisionsDir}/{SampleRunId}.yaml"], readsBack: "run: x\ndecisions:\n");

        var outcome = await _sut.LogAsync(repository, DecisionCategory.Architecture, "appended");

        outcome.Should().Be(DecisionLogOutcome.Recorded);
        repository.Written.Should().ContainSingle().Which.Value.Should()
            .StartWith("run: x").And.Contain("appended");
    }

    /// <summary>
    /// The agent stops listing at SizeLimits.ListFilesMaxEntries, in directory order, before any
    /// sort — and this repository's own decisions directory is past 700 files. At the cap the
    /// listing stops being evidence of anything, so "not listed" may not mean "absent": treating it
    /// as absent would rewrite the file from its header and lose every decision in it.
    /// </summary>
    [Fact]
    public async Task DecisionLog_ListingAtTheEntryCap_IsInconclusiveAndWritesNothing()
    {
        using var scope = _runContext.BeginScope(SampleRunId);
        var capped = Enumerable.Range(0, 1000)
            .Select(i => $"/work/{DecisionsDir}/other-{i}.yaml").ToList();
        var repository = new ListingOnly(capped, readsBack: null);

        var outcome = await _sut.LogAsync(repository, DecisionCategory.Architecture, "would be lost");

        outcome.Should().Be(DecisionLogOutcome.RecordedWithoutRepositoryCopy);
        repository.Written.Should().BeEmpty();
    }

    [Fact]
    public async Task DecisionLog_ListingBelowTheEntryCap_StillStartsAnAbsentFile()
    {
        using var scope = _runContext.BeginScope(SampleRunId);
        var repository = new ListingOnly(
            [$"/work/{DecisionsDir}/someone-else.yaml"], readsBack: null);

        var outcome = await _sut.LogAsync(repository, DecisionCategory.Architecture, "the first one");

        outcome.Should().Be(DecisionLogOutcome.Recorded);
        repository.Written.Should().ContainSingle().Which.Value.Should()
            .StartWith($"run: {SampleRunId}");
    }

    [Fact]
    public async Task DecisionLog_WithNoSurface_MirrorsToTheEventStreamAndWritesNothing()
    {
        using var scope = _runContext.BeginScope(SampleRunId);

        var outcome = await _sut.LogAsync(null, DecisionCategory.Architecture, "no repository here");

        outcome.Should().Be(DecisionLogOutcome.Recorded);
        _events.Events.Should().ContainSingle();
        _repository.Files.Should().BeEmpty();
    }

    [Fact]
    public async Task DecisionLog_WithNoRunScopeAndNoPhaseLabel_WritesNothing()
    {
        var outcome = await _sut.LogAsync(_repository, DecisionCategory.Architecture, "unscoped");

        outcome.Should().Be(DecisionLogOutcome.Recorded);
        _repository.Files.Should().BeEmpty(
            "without a phase label or run scope there is no schema-conformant target");
    }

    [Fact]
    public async Task DecisionLog_EveryDecision_ReachesTheEventStreamWhateverTheSurface()
    {
        using var scope = _runContext.BeginScope(SampleRunId);

        await _sut.LogAsync(_repository, DecisionCategory.Architecture, "with a repository");
        await _sut.LogAsync(null, DecisionCategory.Tooling, "without one");
        await _sut.LogAsync(new ThrowingSurface(), DecisionCategory.TradeOff, "with a broken one");

        _events.Events.Should().HaveCount(3);
    }

    [Fact]
    public async Task DecisionLog_TwoRuns_EachDecisionLandsOnItsOwnRunsSurface()
    {
        var first = new InMemorySandboxFileReader();
        var second = new InMemorySandboxFileReader();

        await Task.WhenAll(
            LogInOwnRunScope("2026-05-20T22-27-43-aaaa", first, "first run's choice"),
            LogInOwnRunScope("2026-05-20T22-27-43-bbbb", second, "second run's choice"));

        first.Files.Should().ContainKey($"{DecisionsDir}/2026-05-20T22-27-43-aaaa.yaml");
        first.Files.Values.Should().NotContain(v => v.Contains("second run's choice"));
        second.Files.Should().ContainKey($"{DecisionsDir}/2026-05-20T22-27-43-bbbb.yaml");
        second.Files.Values.Should().NotContain(v => v.Contains("first run's choice"));
    }

    private async Task LogInOwnRunScope(string runId, ISandboxFileReader repository, string decision)
    {
        using var scope = _runContext.BeginScope(runId);
        await _sut.LogAsync(repository, DecisionCategory.Implementation, decision);
    }

    [Fact]
    public async Task DecisionLog_DecisionWithNewlinesAndQuotes_StaysOneYamlScalar()
    {
        using var scope = _runContext.BeginScope(SampleRunId);

        await _sut.LogAsync(_repository, DecisionCategory.Implementation,
            "chose \"X\" over Y\nbecause Z");

        _repository.Files[RunFile].Should().Contain("chose: \"chose \\\"X\\\" over Y\\nbecause Z\"");
    }

    [Fact]
    public async Task DecisionLog_ConcurrentWrites_AllDecisionsPresent()
    {
        using var scope = _runContext.BeginScope(SampleRunId);

        await Task.WhenAll(Enumerable.Range(1, 10)
            .Select(i => _sut.LogAsync(_repository, DecisionCategory.Implementation, $"decision {i}")));

        for (var i = 1; i <= 10; i++)
            _repository.Files[RunFile].Should().Contain($"decision {i}");
    }

    [Fact]
    public async Task DecisionLog_TicketHashLabel_IsNotAPhase_FallsBackToRunFile()
    {
        using var scope = _runContext.BeginScope(SampleRunId);

        await _sut.LogAsync(_repository, DecisionCategory.Implementation, "ticket-labelled",
            sourceLabel: "#42");

        _repository.Files.Should().ContainKey(RunFile)
            .And.NotContainKey($"{DecisionsDir}/#42.yaml");
    }

    // ---- 2026-09-19-c511b: the sink never ends a run --------------------------------------

    [Fact]
    public async Task DecisionLog_RepositoryWriteThrows_DoesNotThrow()
    {
        using var scope = _runContext.BeginScope(SampleRunId);

        var act = async () => await _sut.LogAsync(
            new ThrowingSurface(), DecisionCategory.Architecture, "the work this describes is done");

        await act.Should().NotThrowAsync(
            "an audit sink that can kill a run is a work step by accident");
    }

    [Fact]
    public async Task DecisionLog_RepositoryWriteThrows_LogsAWarningNamingTheLabelAndTheReason()
    {
        using var scope = _runContext.BeginScope(SampleRunId);
        var log = new CapturingLogger<RepositoryDecisionLogger>();
        var sut = new RepositoryDecisionLogger(
            _runContext, new DecisionEventMirror(_events, _runContext), log);

        await sut.LogAsync(new ThrowingSurface(), DecisionCategory.Architecture, "a choice");

        log.Warnings.Should().ContainSingle()
            .Which.Should().Contain($"{SampleRunId}.yaml").And.Contain("sandbox is gone");
    }

    [Fact]
    public async Task DecisionLog_RepositoryWriteThrows_TheDecisionStillReachesTheEventStream()
    {
        using var scope = _runContext.BeginScope(SampleRunId);

        var outcome = await _sut.LogAsync(
            new ThrowingSurface(), DecisionCategory.Architecture, "a choice");

        outcome.Should().Be(DecisionLogOutcome.RecordedWithoutRepositoryCopy);
        _events.Events.Should().ContainSingle();
    }

    [Fact]
    public async Task DecisionLog_EventMirrorThrows_DoesNotThrowAndTheRunGoesOn()
    {
        using var scope = _runContext.BeginScope(SampleRunId);
        var sut = Logger(new ThrowingEventPublisher());

        var act = async () => await sut.LogAsync(_repository, DecisionCategory.Architecture, "a choice");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DecisionLog_EventMirrorThrows_ReportsTheDecisionAsRecordedNowhere()
    {
        using var scope = _runContext.BeginScope(SampleRunId);
        var sut = Logger(new ThrowingEventPublisher());

        var outcome = await sut.LogAsync(_repository, DecisionCategory.Architecture, "a choice");

        outcome.Should().Be(DecisionLogOutcome.NotRecorded,
            "the run's stream is the record the dashboard reads; the file is a copy of it");
        _repository.Files.Should().BeEmpty("nothing is copied from a decision that was not recorded");
    }

    [Fact]
    public async Task DecisionLog_TokenCancelled_StillPropagatesTheCancellation()
    {
        using var scope = _runContext.BeginScope(SampleRunId);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var act = async () => await _sut.LogAsync(
            new ThrowingSurface(), DecisionCategory.Architecture, "a choice", cancelled.Token);

        await act.Should().ThrowAsync<OperationCanceledException>(
            "a sink that ate a cancel would keep a cancelled run walking for one more tool call");
    }

    // ---- doubles ---------------------------------------------------------------------------

    /// <summary>A surface whose every call fails the way a dead sandbox does.</summary>
    private sealed class ThrowingSurface : ISandboxFileReader
    {
        public Task<bool> ExistsAsync(string path, CancellationToken ct) => throw Dead();
        public Task<string?> TryReadAsync(string path, CancellationToken ct) => throw Dead();
        public Task<string> ReadRequiredAsync(string path, CancellationToken ct) => throw Dead();
        public Task WriteAsync(string path, string content, CancellationToken ct) => throw Dead();
        public Task<IReadOnlyList<string>> ListAsync(string path, int? maxDepth, CancellationToken ct)
            => throw Dead();

        private static IOException Dead() => new("the sandbox is gone");
    }

    /// <summary>
    /// Lists the file, then refuses to read it — what the surface does for a file over the read
    /// limit or one that is not UTF-8, and what a blind read-modify-write would mistake for absent.
    /// </summary>
    private sealed class UnreadableFile(string path) : ISandboxFileReader
    {
        public List<string> Written { get; } = [];

        public Task<bool> ExistsAsync(string p, CancellationToken ct) => Task.FromResult(false);
        public Task<string?> TryReadAsync(string p, CancellationToken ct) => Task.FromResult<string?>(null);
        public Task<string> ReadRequiredAsync(string p, CancellationToken ct) => throw new FileNotFoundException(p);

        public Task WriteAsync(string p, string content, CancellationToken ct)
        {
            Written.Add(p);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> ListAsync(string p, int? maxDepth, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>([path]);
    }

    /// <summary>Answers a fixed listing, and reads back what it was given (or refuses to).</summary>
    private sealed class ListingOnly(IReadOnlyList<string> listing, string? readsBack) : ISandboxFileReader
    {
        public List<KeyValuePair<string, string>> Written { get; } = [];

        public Task<bool> ExistsAsync(string p, CancellationToken ct) =>
            Task.FromResult(readsBack is not null);

        public Task<string?> TryReadAsync(string p, CancellationToken ct) => Task.FromResult(readsBack);

        public Task<string> ReadRequiredAsync(string p, CancellationToken ct) =>
            readsBack is null ? throw new FileNotFoundException(p) : Task.FromResult(readsBack);

        public Task WriteAsync(string p, string content, CancellationToken ct)
        {
            Written.Add(new KeyValuePair<string, string>(p, content));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> ListAsync(string p, int? maxDepth, CancellationToken ct) =>
            Task.FromResult(listing);
    }

    private sealed class ThrowingEventPublisher : IEventPublisher
    {
        public Task PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("the event stream is unavailable");
    }
}
