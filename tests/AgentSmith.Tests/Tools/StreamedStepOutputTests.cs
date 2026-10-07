using AgentSmith.Application.Services.Tools;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;

namespace AgentSmith.Tests.Tools;

/// <summary>
/// 2026-10-07-6b9db: one flag shared by both streams stopped stdout and stderr together once
/// either filled its 1 MB buffer, and the buffer kept only the head. Each stream now keeps its
/// own head, rolling tail, count and flag.
/// </summary>
public sealed class StreamedStepOutputTests
{
    [Fact]
    public void StreamedStepOutput_StdoutFull_StderrStillCollected()
    {
        var sut = new StreamedStepOutput();

        foreach (var line in BoundedRunCommandOutputTests.Lines("first", 1_200_000, "last"))
            sut.Collector.Report(Line(StepEventKind.Stdout, line));
        sut.Collector.Report(Line(StepEventKind.Stderr, "a late error"));

        sut.Stdout.Truncated.Should().BeTrue();
        sut.Stderr.Truncated.Should().BeFalse();
        sut.Stderr.Head.Should().Be("a late error\n");
        sut.Stdout.Tail.Should().EndWith("last\n", "stdout past its head still keeps its last lines");
    }

    [Fact]
    public void StreamedStepOutput_StderrPast1MB_KeepsLastLines()
    {
        var sut = new StreamedStepOutput();
        var lines = BoundedRunCommandOutputTests.Lines("first", 1_500_000, "Tests: 3 failed");

        foreach (var line in lines) sut.Collector.Report(Line(StepEventKind.Stderr, line));

        sut.Stderr.Truncated.Should().BeTrue();
        sut.Stderr.Total.Should().Be(lines.Sum(l => (long)l.Length + 1));
        sut.Stderr.Head.Should().StartWith("first\n");
        sut.Stderr.Tail.Should().EndWith("Tests: 3 failed\n");
        sut.Stderr.Tail.Length.Should().BeLessThanOrEqualTo(StreamCapture.TailMaxChars, "memory stays bounded");
        sut.Stderr.Text.Should().EndWith("(output truncated at 1 MB)", "the program render reads the head as before");
    }

    [Fact]
    public void StreamCapture_After_ReturnsOnlyWhatFollowsThePosition()
    {
        var sut = new StreamCapture();
        sut.Append("abc");
        sut.Append("def");

        sut.After(4).Should().Be("def\n");
        sut.After(100).Should().BeEmpty();
    }

    [Fact]
    public void StreamCapture_LineWiderThanTheTail_KeepsItsLastCharacters()
    {
        var sut = new StreamCapture();
        sut.Append(new string('h', SizeLimits.RunCommandMaxBufferBytes));
        sut.Append(new string('t', StreamCapture.TailMaxChars * 2) + "END");

        sut.Tail.Length.Should().Be(StreamCapture.TailMaxChars);
        sut.Tail.Should().EndWith("END\n");
        sut.After(sut.Total - 4).Should().Be("END\n");
    }

    private static StepEvent Line(StepEventKind kind, string line) =>
        new(StepEvent.CurrentSchemaVersion, Guid.Empty, kind, line, DateTimeOffset.UtcNow);
}
