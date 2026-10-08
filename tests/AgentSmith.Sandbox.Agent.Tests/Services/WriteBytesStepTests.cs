using AgentSmith.Sandbox.Agent.Services;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Sandbox.Agent.Tests.Services;

/// <summary>
/// 2026-10-08-e8b9j: WriteBytes on the sandbox agent — validated by the loop before anything
/// runs, decoded in C#, chunks appended at a temporary path and moved into place by the last.
/// </summary>
public sealed class WriteBytesStepTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"wb-{Guid.NewGuid():N}");

    public WriteBytesStepTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best-effort */ }
    }

    [Fact]
    public async Task JobLoop_WriteBytesWithoutContent_ValidationFailure()
    {
        var bus = new Mock<IRedisJobBus>();
        var queue = new Queue<Step>([
            new Step(WireProtocol.Current, Guid.NewGuid(), StepKind.WriteBytes, Path: Path.Combine(_dir, "a.bin")),
            Step.Shutdown(Guid.NewGuid()),
        ]);
        bus.Setup(b => b.WaitForStepAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => queue.Count > 0 ? queue.Dequeue() : null);
        StepResult? answered = null;
        bus.Setup(b => b.PushResultAsync("job-1", It.IsAny<StepResult>(), It.IsAny<CancellationToken>()))
            .Callback((string _, StepResult r, CancellationToken _) => answered = r)
            .Returns(Task.CompletedTask);
        var executor = new Mock<IStepExecutor>();

        await new JobLoop(bus.Object, new ToleratedStepReader(bus.Object, NullLogger<ToleratedStepReader>.Instance),
            executor.Object, NullStepInFlightMarker.Instance, NullLogger<JobLoop>.Instance).RunAsync("job-1", CancellationToken.None);

        answered!.ErrorMessage.Should().Contain("WriteBytes step requires non-null Content");
        executor.Verify(e => e.ExecuteAsync(It.IsAny<Step>(), It.IsAny<Func<IReadOnlyList<StepEvent>, Task>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Agent_LastChunk_RenamesIntoPlace()
    {
        var temp = Path.Combine(_dir, "logo.png.tmp.1");
        var target = Path.Combine(_dir, "img", "logo.png");

        var first = await Execute(Chunk(temp, [1, 2, 3], append: false, renameTo: null));
        File.Exists(target).Should().BeFalse("a reader never sees half a file");
        var last = await Execute(Chunk(temp, [4, 5], append: true, renameTo: target));

        first.ExitCode.Should().Be(0);
        last.ExitCode.Should().Be(0);
        (await File.ReadAllBytesAsync(target)).Should().Equal(1, 2, 3, 4, 5);
        File.Exists(temp).Should().BeFalse();
    }

    [Fact]
    public async Task Agent_ChunkOverFourMegabytes_Refused()
    {
        var result = await Execute(Chunk(Path.Combine(_dir, "big.bin"), new byte[SizeLimits.WriteBytesChunkMaxBytes + 1], false, null));

        result.ExitCode.Should().Be(1);
        result.ErrorMessage.Should().Contain("one step may carry");
    }

    private static Step Chunk(string path, byte[] bytes, bool append, string? renameTo) =>
        new(WireProtocol.Current, Guid.NewGuid(), StepKind.WriteBytes, Path: path,
            Content: Convert.ToBase64String(bytes), Append: append, RenameTo: renameTo);

    private static Task<StepResult> Execute(Step step) =>
        new StepExecutor(Mock.Of<IProcessRunner>(), new FileStepHandler(NullLogger<FileStepHandler>.Instance),
                new GrepStepHandler(Mock.Of<IProcessRunner>(), NullLogger<GrepStepHandler>.Instance),
                new DirectoryTreeStepHandler(NullLogger<DirectoryTreeStepHandler>.Instance), NullLogger<StepExecutor>.Instance)
            .ExecuteAsync(step, _ => Task.CompletedTask, CancellationToken.None);
}
