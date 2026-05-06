using System.Text.Json;
using AgentSmith.Sandbox.Agent.Services;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Sandbox.Agent.Tests.Services;

public sealed class GrepStepHandlerTests : IDisposable
{
    private readonly string _root;

    public GrepStepHandlerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose() { try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ } }

    [Fact]
    public async Task HandleAsync_ManagedFallback_FindsLineWithRegexMatch()
    {
        File.WriteAllText(Path.Combine(_root, "a.cs"), "namespace X;\n// TODO fix this\nclass A { }");
        var handler = BuildHandlerNoRipgrep();
        var step = new Step(1, Guid.NewGuid(), StepKind.Grep, Path: _root, Pattern: "TODO");

        var result = await handler.HandleAsync(step, _ => Task.CompletedTask, CancellationToken.None);

        result.ExitCode.Should().Be(0);
        var matches = JsonSerializer.Deserialize<List<JsonElement>>(result.OutputContent!)!;
        matches.Should().HaveCount(1);
        matches[0].GetProperty("line").GetInt32().Should().Be(2);
        matches[0].GetProperty("text").GetString().Should().Contain("TODO");
    }

    [Fact]
    public async Task HandleAsync_RespectsMaxMatches_AndEmitsTruncationEvent()
    {
        File.WriteAllText(Path.Combine(_root, "a.cs"),
            string.Join('\n', Enumerable.Range(0, 10).Select(_ => "TODO")));
        var handler = BuildHandlerNoRipgrep();
        var step = new Step(1, Guid.NewGuid(), StepKind.Grep,
            Path: _root, Pattern: "TODO", MaxMatches: 3);
        var truncationEvents = new List<StepEvent>();

        var result = await handler.HandleAsync(step, evs =>
        {
            truncationEvents.AddRange(evs);
            return Task.CompletedTask;
        }, CancellationToken.None);

        var matches = JsonSerializer.Deserialize<List<JsonElement>>(result.OutputContent!)!;
        matches.Should().HaveCount(3);
        truncationEvents.Should().Contain(e => e.Line.Contains("truncated") && e.Line.Contains("3"));
    }

    [Fact]
    public async Task HandleAsync_NoMatches_ReturnsEmptyArray()
    {
        File.WriteAllText(Path.Combine(_root, "a.cs"), "no targets here");
        var handler = BuildHandlerNoRipgrep();
        var step = new Step(1, Guid.NewGuid(), StepKind.Grep, Path: _root, Pattern: "absent");

        var result = await handler.HandleAsync(step, _ => Task.CompletedTask, CancellationToken.None);

        result.ExitCode.Should().Be(0);
        var matches = JsonSerializer.Deserialize<List<JsonElement>>(result.OutputContent!)!;
        matches.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_BinaryFileSkipped_ManagedFallback()
    {
        File.WriteAllBytes(Path.Combine(_root, "blob.bin"), Enumerable.Repeat((byte)0xff, 2_000_000).ToArray());
        File.WriteAllText(Path.Combine(_root, "a.cs"), "TODO this");
        var handler = BuildHandlerNoRipgrep();
        var step = new Step(1, Guid.NewGuid(), StepKind.Grep, Path: _root, Pattern: "TODO");

        var result = await handler.HandleAsync(step, _ => Task.CompletedTask, CancellationToken.None);

        var matches = JsonSerializer.Deserialize<List<JsonElement>>(result.OutputContent!)!;
        matches.Should().HaveCount(1, "the >1MB binary file is skipped by the managed fallback");
    }

    private static GrepStepHandler BuildHandlerNoRipgrep()
    {
        var runnerMock = new Mock<IProcessRunner>();
        runnerMock.Setup(r => r.RunAsync(
                It.Is<Step>(s => s.Command == "rg" && s.Args!.Contains("--version")),
                It.IsAny<Action<StepEventKind, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessOutcome(127, false, "rg not found"));
        return new GrepStepHandler(runnerMock.Object, NullLogger<GrepStepHandler>.Instance);
    }
}
