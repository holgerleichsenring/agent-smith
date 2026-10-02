using System.Text.Json;
using AgentSmith.Sandbox.Agent.Services;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Sandbox.Agent.Tests.Services;

/// <summary>
/// 2026-10-01-283df: the uploaded websites a run carries sit at .agentsmith/reference under the
/// repository root. A search or a tree that starts outside skips them — both grep engines and
/// directory_tree alike — and one that starts inside reads them.
/// </summary>
public sealed class ReferenceDirectoryScopeTests : IDisposable
{
    private readonly string _repo = Path.Combine(Path.GetTempPath(), "as-283df-" + Guid.NewGuid().ToString("N"));

    public ReferenceDirectoryScopeTests()
    {
        Directory.CreateDirectory(Path.Combine(_repo, "src"));
        Directory.CreateDirectory(Path.Combine(_repo, ".agentsmith", "reference", "set-1", "css"));
        File.WriteAllText(Path.Combine(_repo, "src", "app.css"), "color: #c0ffee;");
        File.WriteAllText(Path.Combine(_repo, ".agentsmith", "reference", "set-1", "css", "site.css"), "color: #c0ffee;");
    }

    public void Dispose() { try { Directory.Delete(_repo, recursive: true); } catch { /* best effort */ } }

    private string References => Path.Combine(_repo, ".agentsmith", "reference");

    [Fact]
    public async Task Grep_FromRepoRoot_SkipsReferenceDirectory_FromInsideIt_SearchesIt_Managed()
    {
        var fromRoot = await ManagedAsync(_repo);
        var fromInside = await ManagedAsync(References);

        fromRoot.Should().Equal("src/app.css");
        fromInside.Should().Equal("set-1/css/site.css");
    }

    [Fact]
    public async Task Grep_FromRepoRoot_SkipsReferenceDirectory_FromInsideIt_SearchesIt_Ripgrep()
    {
        var (rootArgs, rootCwd) = await RipgrepAsync(_repo);
        var (insideArgs, _) = await RipgrepAsync(References);

        rootArgs.Should().Contain("!/.agentsmith/reference/**");
        rootCwd.Should().Be(_repo, "the anchored glob is matched against rg's working directory");
        insideArgs.Should().NotContain("!/.agentsmith/reference/**", "a search rooted inside asked for it");
    }

    [Fact]
    public async Task DirectoryTree_FromRepoRoot_OmitsReferenceDirectory_FromInsideIt_ListsIt()
    {
        var handler = new DirectoryTreeStepHandler(NullLogger<DirectoryTreeStepHandler>.Instance);

        var fromRoot = await handler.HandleAsync(Step(StepKind.DirectoryTree, Path.Combine(_repo, ".agentsmith")),
            _ => Task.CompletedTask, CancellationToken.None);
        var fromInside = await handler.HandleAsync(Step(StepKind.DirectoryTree, References),
            _ => Task.CompletedTask, CancellationToken.None);

        fromRoot.OutputContent.Should().NotContain("reference/", "the root lies outside the reference directory");
        fromInside.OutputContent.Should().Contain("site.css");
    }

    [Fact]
    public void GrepScope_Summary_NamesTheReferenceDirectory()
    {
        GrepScope.Summary.Should().Contain(".agentsmith/reference");
        GrepScope.SkipsReferences("/work/src", "/work").Should().BeTrue();
        GrepScope.SkipsReferences("/work/.agentsmith/reference/set-1", "/work").Should().BeFalse();
        GrepScope.IsUnderReferences("/work/.agentsmith/reference/set-1/a.css", "/work").Should().BeTrue();
        GrepScope.IsUnderReferences("/work/sub/.agentsmith/reference/a.css", "/work").Should().BeFalse("the exclusion is rooted");
    }

    private Step Step(StepKind kind, string path) =>
        new(1, Guid.NewGuid(), kind, Path: path, Pattern: kind == StepKind.Grep ? "c0ffee" : null,
            OutputMode: GrepOutputMode.FilesWithMatches, WorkingDirectory: _repo);

    private async Task<List<string>> ManagedAsync(string root)
    {
        var runner = new Mock<IProcessRunner>();
        runner.Setup(r => r.RunAsync(It.IsAny<Step>(), It.IsAny<Action<StepEventKind, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessOutcome(127, false, "rg not found"));
        var result = await new GrepStepHandler(runner.Object, NullLogger<GrepStepHandler>.Instance)
            .HandleAsync(Step(StepKind.Grep, root), _ => Task.CompletedTask, CancellationToken.None);
        return [.. JsonSerializer.Deserialize<List<JsonElement>>(result.OutputContent!)!
            .Select(row => row.GetProperty("path").GetString()!.Replace('\\', '/'))];
    }

    private async Task<(List<string> Args, string? Cwd)> RipgrepAsync(string root)
    {
        var args = new List<string>();
        string? cwd = null;
        var runner = new Mock<IProcessRunner>();
        runner.Setup(r => r.RunAsync(It.Is<Step>(s => s.Args!.Contains("--version")),
                It.IsAny<Action<StepEventKind, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessOutcome(0, false, null));
        runner.Setup(r => r.RunAsync(It.Is<Step>(s => !s.Args!.Contains("--version")),
                It.IsAny<Action<StepEventKind, string>>(), It.IsAny<CancellationToken>()))
            .Callback((Step s, Action<StepEventKind, string> _, CancellationToken _) =>
            {
                args.AddRange(s.Args!);
                cwd = s.WorkingDirectory;
            })
            .ReturnsAsync(new ProcessOutcome(1, false, null));
        await new GrepStepHandler(runner.Object, NullLogger<GrepStepHandler>.Instance)
            .HandleAsync(Step(StepKind.Grep, root), _ => Task.CompletedTask, CancellationToken.None);
        return (args, cwd);
    }
}
