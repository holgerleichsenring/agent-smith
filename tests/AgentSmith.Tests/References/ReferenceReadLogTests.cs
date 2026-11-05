using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;

namespace AgentSmith.Tests.References;

/// <summary>2026-11-04-96a0c: what read_file opened of an upload in one turn — successful reads only, set-relative.</summary>
public sealed class ReferenceReadLogTests
{
    [Fact]
    public void ReferenceReadLog_ASuccessfulReadFile_IsRecordedNormalised()
    {
        var log = new ReferenceReadLog();

        log.Record(Read("./app/a.cs"), Done(0));
        log.Record(Read("/app/b.cs"), Done(0));

        log.Paths.Should().Equal("app/a.cs", "app/b.cs");
    }

    [Fact]
    public void ReferenceReadLog_ARootedWorkPath_IsRecordedRelative()
    {
        var log = new ReferenceReadLog();

        log.Record(Read("/work/app/a.cs"), Done(0));

        log.Paths.Should().Equal("app/a.cs");
    }

    [Fact]
    public async Task ReferenceReadLog_ConcurrentReads_AreAllRecorded()
    {
        var log = new ReferenceReadLog();

        await Task.WhenAll(Enumerable.Range(0, 200).Select(i => Task.Run(() => log.Record(Read($"f{i}.cs"), Done(0)))));

        log.Paths.Should().HaveCount(200, "the master and its children read through one sandbox at once");
    }

    [Fact]
    public void ReferenceReadLog_AFailedReadOrAGrepOrARun_IsNotRecorded()
    {
        var log = new ReferenceReadLog();

        log.Record(Read("app/big.bin"), Done(1));
        log.Record(new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Grep, Path: "app/a.cs"), Done(0));
        log.Record(new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run, Path: "app/a.cs"), Done(0));

        log.Paths.Should().BeEmpty("a match or a command's output shows a fragment, not the file");
    }

    private static Step Read(string path) => new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.ReadFile, Path: path);

    private static StepResult Done(int exitCode) =>
        new(StepResult.CurrentSchemaVersion, Guid.NewGuid(), exitCode, false, 0.1, exitCode == 0 ? null : "refused");
}
