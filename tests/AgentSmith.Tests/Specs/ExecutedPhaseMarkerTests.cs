using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.PhaseExecution;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-08-4aa9: the executed-phase record is this system's own commit on the spec
/// path, so the pointer moves to the sha the write returned — carrying the hand-back
/// state, as the publisher's pointer does. A write that failed committed nothing and
/// moves nothing.
/// </summary>
public sealed class ExecutedPhaseMarkerTests
{
    private const string Key = "azdo-1";
    private const string Project = "sample";
    private static readonly RepoConnection Primary = new() { Name = "primary" };

    [Fact]
    public async Task Mark_AWrittenRecord_MovesThePointerToItsSha()
    {
        var pointers = await PointersAt("spec-sha-1");
        var writer = new StubWriter(SpecSetWriteResult.Ok("marker-sha-2"));
        var pipeline = Pipeline();

        await Marker(writer, pointers).MarkAsync(pipeline, [Primary], Draft("p1a"), default);

        var pointer = await pointers.GetAsync(Project, Key, default);
        pointer!.RevisionSha.Should().Be("marker-sha-2", "the record is our commit on the spec path");
        pointer.RevisionNumber.Should().Be(1, "the record adds no revision");
        pointer.CarryingRepo.Should().Be("primary");
        pointer.LastHandbackCase.Should().Be(SpecHandbackCase.Question, "the hand-back step owns the counters");
        pointer.RepeatedHandbackCount.Should().Be(1);
        writer.Seen!.Executed.Should().Equal("p1a");
        pipeline.Get<SpecSet>(ContextKeys.SpecSet).Executed.Should().Equal("p1a");
    }

    [Fact]
    public async Task Mark_AFailedWrite_LeavesThePointer()
    {
        var pointers = await PointersAt("spec-sha-1");
        var writer = new StubWriter(SpecSetWriteResult.Failed("no sandbox available"));

        await Marker(writer, pointers).MarkAsync(Pipeline(), [Primary], Draft("p1a"), default);

        (await pointers.GetAsync(Project, Key, default))!.RevisionSha.Should().Be("spec-sha-1",
            "nothing was committed, so the last commit this system wrote is unchanged");
    }

    private static ExecutedPhaseMarker Marker(ISpecSetWriter writer, ISpecSetPointerStore pointers) => new(
        writer,
        new SpecSetPointerRecorder(pointers, NullLogger<SpecSetPointerRecorder>.Instance),
        NullLogger<ExecutedPhaseMarker>.Instance);

    private static async Task<InMemorySpecSetPointerStore> PointersAt(string sha)
    {
        var pointers = new InMemorySpecSetPointerStore();
        await pointers.SaveAsync(
            Project, new SpecSetPointer(Key, "primary", sha, 1, SpecHandbackCase.Question, 1), default);
        return pointers;
    }

    private static PipelineContext Pipeline()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ProjectName, Project);
        pipeline.Set(ContextKeys.SpecRepo, "primary");
        pipeline.Set(ContextKeys.SpecSet, new SpecSet(
            Key, [Phase("p1a"), Phase("p1b")], SpecAccounting.Empty,
            [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow)],
            SpecSource.BranchArtifact));
        return pipeline;
    }

    private static PhaseDraft Draft(string id) =>
        new(id, $"Goal {id}", $"phase: {id}\ngoal: \"Goal {id}\"", []) { Done = [$"Done {id}."] };

    private static SpecPhase Phase(string id) => new(Draft(id), id, string.Empty, []);

    private sealed class StubWriter(SpecSetWriteResult result) : ISpecSetWriter
    {
        public SpecSet? Seen { get; private set; }

        public Task<SpecSetWriteResult> WriteAsync(
            PipelineContext pipeline, RepoConnection carryingRepo, SpecSet set, CancellationToken ct)
        {
            Seen = set;
            return Task.FromResult(result);
        }
    }
}
