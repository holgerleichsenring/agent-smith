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
    public async Task HandleAsync_RespectsHeadLimit_AndEmitsTruncationEvent()
    {
        File.WriteAllText(Path.Combine(_root, "a.cs"),
            string.Join('\n', Enumerable.Range(0, 10).Select(_ => "TODO")));
        var handler = BuildHandlerNoRipgrep();
        var step = new Step(1, Guid.NewGuid(), StepKind.Grep,
            Path: _root, Pattern: "TODO", HeadLimit: 3);
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

    [Fact]
    public async Task HandleAsync_PathIsSpecificFile_ScansThatFileOnly()
    {
        // Skills routinely call grep with a specific file (cited from an upstream
        // observation), not a directory. The managed fallback used to throw
        // DirectoryNotFoundException because Directory.EnumerateFiles requires a
        // directory; the fix detects File.Exists and scans the file directly.
        var filePath = Path.Combine(_root, "Controller.cs");
        File.WriteAllText(filePath, "namespace X;\npublic Result CreateApplication() { return Ok(result); }\n");
        var handler = BuildHandlerNoRipgrep();
        var step = new Step(1, Guid.NewGuid(), StepKind.Grep, Path: filePath, Pattern: "CreateApplication|return Ok\\(");

        var result = await handler.HandleAsync(step, _ => Task.CompletedTask, CancellationToken.None);

        result.ExitCode.Should().Be(0);
        var matches = JsonSerializer.Deserialize<List<JsonElement>>(result.OutputContent!)!;
        matches.Should().HaveCount(1);
        matches[0].GetProperty("line").GetInt32().Should().Be(2);
        matches[0].GetProperty("path").GetString().Should().Be("Controller.cs");
    }

    // 2026-09-17-042ed: a missing path is a search that could not run, not a search that found
    // nothing — an empty array here was read as proof that a pattern is absent.
    [Fact]
    public async Task ReviewLook_SearchWithInvalidPatternOrMissingPath_IsCouldNotRun()
    {
        var missing = Path.Combine(_root, "does-not-exist", "ghost.cs");
        var handler = BuildHandlerNoRipgrep();

        var noPath = await handler.HandleAsync(
            new Step(1, Guid.NewGuid(), StepKind.Grep, Path: missing, Pattern: "anything"),
            _ => Task.CompletedTask, CancellationToken.None);
        var badPattern = await handler.HandleAsync(
            new Step(1, Guid.NewGuid(), StepKind.Grep, Path: _root, Pattern: "("),
            _ => Task.CompletedTask, CancellationToken.None);

        noPath.ExitCode.Should().Be(1);
        noPath.ErrorMessage.Should().Contain("path not found");
        badPattern.ExitCode.Should().Be(1, "an invalid pattern is a failure, never an empty match list");
    }

    [Theory]
    [InlineData(2)]
    [InlineData(127)]
    public async Task Ripgrep_ErrorExit_IsAFailureNotAnEmptyMatchList(int exit)
    {
        var (handler, _) = BuildHandlerWithRipgrep(exit);

        var result = await handler.HandleAsync(
            new Step(1, Guid.NewGuid(), StepKind.Grep, Path: _root, Pattern: "("),
            _ => Task.CompletedTask, CancellationToken.None);

        result.ExitCode.Should().Be(1);
        result.ErrorMessage.Should().Contain($"rg exited {exit}");
    }

    [Fact]
    public async Task Ripgrep_NoMatchExit_IsAnEmptyMatchList()
    {
        var (handler, _) = BuildHandlerWithRipgrep(1);

        var result = await handler.HandleAsync(
            new Step(1, Guid.NewGuid(), StepKind.Grep, Path: _root, Pattern: "absent"),
            _ => Task.CompletedTask, CancellationToken.None);

        result.ExitCode.Should().Be(0);
        result.OutputContent.Should().Be("[]");
    }

    // 2026-09-17-042ed: the review's search reads dotfiles and ignored paths BECAUSE an absence
    // it states must hold over the whole checkout. Every other grep — every master's
    // grep_in_tree — keeps the repository's own ignore rules, so a checked-out .venv, a target/
    // or an .env does not flood a result head that was asked about source.
    [Fact]
    public async Task Ripgrep_SearchesHiddenAndIgnoredPaths_OnlyWhenTheStepAsks()
    {
        var (asking, askingArgs) = BuildHandlerWithRipgrep(1);
        var (ordinary, ordinaryArgs) = BuildHandlerWithRipgrep(1);

        await asking.HandleAsync(
            new Step(1, Guid.NewGuid(), StepKind.Grep, Path: _root, Pattern: "TOKEN", SearchHidden: true),
            _ => Task.CompletedTask, CancellationToken.None);
        await ordinary.HandleAsync(
            new Step(1, Guid.NewGuid(), StepKind.Grep, Path: _root, Pattern: "TOKEN"),
            _ => Task.CompletedTask, CancellationToken.None);

        askingArgs.Should().Contain(["--hidden", "--no-ignore"]);
        ordinaryArgs.Should().NotContain("--hidden").And.NotContain("--no-ignore");
        ordinaryArgs.Should().ContainInConsecutiveOrder("--glob", "!**/node_modules/**");
        ordinaryArgs.Should().ContainInConsecutiveOrder("-e", "TOKEN", _root);
    }

    // The managed fallback reads a dotfile whatever the step asked for, which is why the flag
    // exists on the engine that would otherwise skip it.
    [Fact]
    public async Task ManagedFallback_ReadsADotfile()
    {
        File.WriteAllText(Path.Combine(_root, ".env"), "TOKEN=1");

        var managed = await BuildHandlerNoRipgrep().HandleAsync(
            new Step(1, Guid.NewGuid(), StepKind.Grep, Path: _root, Pattern: "TOKEN"),
            _ => Task.CompletedTask, CancellationToken.None);

        JsonSerializer.Deserialize<List<JsonElement>>(managed.OutputContent!)!
            .Should().ContainSingle(m => m.GetProperty("path").GetString() == ".env");
    }

    // rg exits 2 for a PARTIAL error — one unreadable file, a dangling symlink — after printing
    // every match it did find. Discarding those turned a real match list into "nothing here".
    [Fact]
    public async Task Ripgrep_PartialError_KeepsTheMatchesAndTellsTheError()
    {
        var (handler, _) = BuildHandlerWithRipgrep(2, stdout: [MatchJson("a.cs", 3, "TOKEN=1")]);
        var events = new List<StepEvent>();

        var result = await handler.HandleAsync(
            new Step(1, Guid.NewGuid(), StepKind.Grep, Path: _root, Pattern: "TOKEN"),
            e => { events.AddRange(e); return Task.CompletedTask; }, CancellationToken.None);

        result.ExitCode.Should().Be(0, "the matches rg printed are matches it found");
        JsonSerializer.Deserialize<List<JsonElement>>(result.OutputContent!)!.Should().ContainSingle();
        events.Should().ContainSingle(e => e.Kind == StepEventKind.Stderr && e.Line.Contains("rg exited 2"));
    }

    // A search that ran out of time is not the same fault as one that could not start, and a
    // caller can only tell them apart if the flag survives.
    [Fact]
    public async Task Ripgrep_Timeout_StaysATimeout()
    {
        var (handler, _) = BuildHandlerWithRipgrep(124, timedOut: true);

        var result = await handler.HandleAsync(
            new Step(1, Guid.NewGuid(), StepKind.Grep, Path: _root, Pattern: "TOKEN"),
            _ => Task.CompletedTask, CancellationToken.None);

        result.ExitCode.Should().Be(1);
        result.TimedOut.Should().BeTrue();
    }

    // A path the caller NAMED is read wherever it sits: the managed fallback always did, and
    // grep_in_file on vendor/x/y.go must not depend on which engine served the step.
    [Fact]
    public async Task NamedFileInsideAnExcludedDirectory_IsSearchedByBothPaths()
    {
        Directory.CreateDirectory(Path.Combine(_root, "vendor", "x"));
        var named = Path.Combine(_root, "vendor", "x", "y.go");
        File.WriteAllText(named, "const Token = 1");
        var (ripgrep, args) = BuildHandlerWithRipgrep(1);

        var managed = await BuildHandlerNoRipgrep().HandleAsync(
            new Step(1, Guid.NewGuid(), StepKind.Grep, Path: named, Pattern: "Token"),
            _ => Task.CompletedTask, CancellationToken.None);
        await ripgrep.HandleAsync(
            new Step(1, Guid.NewGuid(), StepKind.Grep, Path: named, Pattern: "Token"),
            _ => Task.CompletedTask, CancellationToken.None);

        JsonSerializer.Deserialize<List<JsonElement>>(managed.OutputContent!)!.Should().ContainSingle();
        args.Should().NotContain("!**/vendor/**", "an exclusion may not blank out a path that was named");
    }

    [Fact]
    public async Task ExcludedDirectory_UnderASearchedTree_IsStillSkipped()
    {
        Directory.CreateDirectory(Path.Combine(_root, "vendor", "x"));
        File.WriteAllText(Path.Combine(_root, "vendor", "x", "y.go"), "const Token = 1");
        var (ripgrep, args) = BuildHandlerWithRipgrep(1);

        var managed = await BuildHandlerNoRipgrep().HandleAsync(
            new Step(1, Guid.NewGuid(), StepKind.Grep, Path: _root, Pattern: "Token"),
            _ => Task.CompletedTask, CancellationToken.None);
        await ripgrep.HandleAsync(
            new Step(1, Guid.NewGuid(), StepKind.Grep, Path: _root, Pattern: "Token"),
            _ => Task.CompletedTask, CancellationToken.None);

        JsonSerializer.Deserialize<List<JsonElement>>(managed.OutputContent!)!.Should().BeEmpty();
        args.Should().Contain("!**/vendor/**");
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

    private static (GrepStepHandler Handler, List<string> Args) BuildHandlerWithRipgrep(
        int exit, bool timedOut = false, string[]? stdout = null)
    {
        var args = new List<string>();
        var runnerMock = new Mock<IProcessRunner>();
        runnerMock.Setup(r => r.RunAsync(
                It.Is<Step>(s => s.Command == "rg" && s.Args!.Contains("--version")),
                It.IsAny<Action<StepEventKind, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessOutcome(0, false, null));
        runnerMock.Setup(r => r.RunAsync(
                It.Is<Step>(s => s.Command == "rg" && !s.Args!.Contains("--version")),
                It.IsAny<Action<StepEventKind, string>>(), It.IsAny<CancellationToken>()))
            .Callback((Step s, Action<StepEventKind, string> onLine, CancellationToken _) =>
            {
                args.AddRange(s.Args!);
                foreach (var line in stdout ?? []) onLine(StepEventKind.Stdout, line);
            })
            .ReturnsAsync(new ProcessOutcome(exit, timedOut, exit == 1 ? null : "regex parse error"));
        return (new GrepStepHandler(runnerMock.Object, NullLogger<GrepStepHandler>.Instance), args);
    }

    /// <summary>One ndjson row as ripgrep --json prints a match.</summary>
    private string MatchJson(string file, int line, string text) => JsonSerializer.Serialize(new
    {
        type = "match",
        data = new
        {
            path = new { text = Path.Combine(_root, file) },
            line_number = line,
            lines = new { text = text + "\n" },
        },
    });
}
