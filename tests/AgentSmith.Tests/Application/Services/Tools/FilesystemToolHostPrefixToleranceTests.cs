using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;

namespace AgentSmith.Tests.Tools;

// p0179h: pin the multi-repo-prefix path semantics of FilesystemToolHost.
// In single-sandbox mode the host now strips a matching prefix instead of
// passing it through as a directory name; in multi-sandbox mode the prefix
// is required and a helpful error fires on miss. Combined fix with the
// master prompt teaching the prefix rule.
public sealed class FilesystemToolHostPrefixToleranceTests
{
    [Fact]
    public async Task FilesystemToolHost_SingleRepoWithMatchingPrefix_StripsAndRoutes()
    {
        var sandbox = new PathCapturingSandbox();
        var host = NewHost(("repo-a", sandbox));

        await host.ListDirectory("repo-a/src/Controllers");

        sandbox.LastPath.Should().Be("src/Controllers");
    }

    [Fact]
    public async Task FilesystemToolHost_SingleRepoWithBarePath_RoutesAsBefore()
    {
        var sandbox = new PathCapturingSandbox();
        var host = NewHost(("repo-a", sandbox));

        await host.ListDirectory("src/Controllers");

        sandbox.LastPath.Should().Be("src/Controllers");
    }

    [Fact]
    public async Task FilesystemToolHost_MultiRepoWithPrefix_RoutesAsBefore()
    {
        var a = new PathCapturingSandbox();
        var b = new PathCapturingSandbox();
        var host = NewHost(("repo-a", a), ("repo-b", b));

        await host.ListDirectory("repo-b/src/Models");

        b.LastPath.Should().Be("src/Models");
        a.LastPath.Should().BeNull("the other sandbox must not be called");
    }

    [Fact]
    public async Task FilesystemToolHost_MultiRepoMissingPrefix_ReturnsHelpfulError()
    {
        // p0259b: a missing repo prefix returns a recoverable tool-error string the
        // LLM can act on (retry with a prefix) — it no longer throws and aborts the run.
        var a = new PathCapturingSandbox();
        var b = new PathCapturingSandbox();
        var host = NewHost(("repo-a", a), ("repo-b", b));

        var result = await host.ListDirectory("src/Models");

        result.Should().StartWith("Error");
        result.Should().Contain("repo-a").And.Contain("repo-b");
    }

    [Fact]
    public async Task FilesystemToolHost_SingleRepoWithNonMatchingFirstSegment_PassesThrough()
    {
        // A bare path whose first segment happens not to match the repo name is
        // routed as-is (operator-friendly fallback for CLI-pasted absolute-style
        // paths in single-repo mode).
        var sandbox = new PathCapturingSandbox();
        var host = NewHost(("repo-a", sandbox));

        await host.ListDirectory("foo/Controllers");

        sandbox.LastPath.Should().Be("foo/Controllers");
    }

    private static FilesystemToolHost NewHost(params (string Name, ISandbox Sandbox)[] sandboxes)
    {
        var dict = sandboxes.ToDictionary(s => s.Name, s => s.Sandbox);
        return new FilesystemToolHost(dict, sandboxes[0].Name);
    }

    private sealed class PathCapturingSandbox : ISandbox
    {
        public string JobId => "test";
        public string? LastPath { get; private set; }

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
        {
            LastPath = step.Path;
            return Task.FromResult(new StepResult(
                SchemaVersion: StepResult.CurrentSchemaVersion,
                StepId: step.StepId,
                ExitCode: 0,
                TimedOut: false,
                DurationSeconds: 0,
                ErrorMessage: null,
                OutputContent: "[]"));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
