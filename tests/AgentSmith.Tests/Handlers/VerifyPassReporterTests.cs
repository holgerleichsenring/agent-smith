using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Verify;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-11-17-d6fbc: a verify pass reaches the dashboard as data — per repository its stages in order and whether they
/// came out green, and the contexts whose derivation moved — while the step's prose stays as it was.
/// </summary>
public sealed class VerifyPassReporterTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 17, 9, 0, 0, TimeSpan.Zero);

    private static VerifyPassReporter Reporter(IEventPublisher events) =>
        new(events, TimeProvider.System, NullLogger<VerifyPassReporter>.Instance);

    [Fact]
    public void Build_AGreenPass_ListsEachRepositoryWithItsStagesInOrder()
    {
        var outcomes = new List<VerifyOutcome>
        {
            new("server", "install", "dotnet restore", 0, false),
            new("server", "build", "dotnet build", 0, false),
            new("server", "test", "dotnet test", 0, false),
            new("", "build", "pnpm build", 0, false),
        };

        var pass = Reporter(new RecordingEventPublisher()).Build("r1", outcomes, new VerifyResolutionNotes(), passed: true, At);

        pass.Result.Should().Be(VerifyPassResults.Passed);
        pass.Repositories.Select(r => r.Repository).Should().Equal("server", "(default)");
        pass.Repositories[0].Stages.Select(s => s.Stage).Should().Equal("install", "build", "test");
        pass.Repositories.Should().OnlyContain(r => r.Result == VerifyPassResults.Passed);
        pass.StaleDerivations.Should().BeEmpty();
    }

    [Fact]
    public void Build_ARedStage_FailsItsRepositoryAndThePass()
    {
        var outcomes = new List<VerifyOutcome>
        {
            new("server", "build", "dotnet build", 0, false),
            new("server", "test", "dotnet test", 1, false),
            new("client", "test", "pnpm test", 0, true),
        };

        var pass = Reporter(new RecordingEventPublisher()).Build("r1", outcomes, new VerifyResolutionNotes(), passed: false, At);

        pass.Result.Should().Be(VerifyPassResults.Failed);
        pass.Repositories.Single(r => r.Repository == "server").Result.Should().Be(VerifyPassResults.Failed);
        pass.Repositories.Single(r => r.Repository == "client").Stages.Single().Skipped.Should().BeTrue();
    }

    [Fact]
    public async Task ReportAsync_StaleDerivations_RideTheEventAsData()
    {
        var notes = new VerifyResolutionNotes();
        notes.DerivationMoved("server", "default", [".github/workflows/ci.yml"]);
        notes.DerivationMoved("server", "web", ["package.json", "pnpm-lock.yaml"]);
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "r1");
        var events = new RecordingEventPublisher();

        await Reporter(events).ReportAsync(pipeline, [], notes, passed: true, CancellationToken.None);

        var pass = events.Events.Should().ContainSingle().Which.Should().BeOfType<VerifyPassRecordedEvent>().Subject;
        pass.RunId.Should().Be("r1");
        pass.StaleDerivations.Should().Equal(
            new[] { new VerifyStaleDerivation("server", "default", [".github/workflows/ci.yml"]),
                new VerifyStaleDerivation("server", "web", ["package.json", "pnpm-lock.yaml"]) },
            (l, r) => l.Repository == r.Repository && l.Context == r.Context && l.Files.SequenceEqual(r.Files));
    }

    [Fact]
    public async Task ReportAsync_NoRunId_PublishesNothing()
    {
        var events = new RecordingEventPublisher();

        await Reporter(events).ReportAsync(new PipelineContext(), [], new VerifyResolutionNotes(), passed: true, CancellationToken.None);

        events.Events.Should().BeEmpty();
    }

    [Fact]
    public void DerivationMoved_KeepsTheProseAndRecordsTheData()
    {
        var notes = new VerifyResolutionNotes();

        notes.DerivationMoved("", "default", ["ci.yml"]);

        notes.Stale.Should().ContainSingle().Which.Should().StartWith("(default): context 'default' derived its verify block from [ci.yml]");
        notes.StaleDerivations.Should().ContainSingle().Which.Context.Should().Be("default");
    }
}
