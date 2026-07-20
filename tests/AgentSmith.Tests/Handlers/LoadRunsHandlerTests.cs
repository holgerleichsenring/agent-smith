using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

// p0355: the run-history loader is the trust gate for prior-run records — a
// run that aborted at bootstrap (repo empty/renamed) recorded a confused
// result and must not be fed back into a later run's context as authoritative.
public sealed class LoadRunsHandlerTests
{
    private const string RunsDir = "/work/.agentsmith/runs";

    private const string HealthyResult =
        "---\nresult: success\n---\n\nImplemented the retry policy in the poller.";

    private const string BootstrapAbortedResult =
        "---\nresult: failed\n---\n\n## Outcome\n\n⚠️ **This run did not complete.** "
        + "Pipeline aborted: missing bootstrap in repos: [server]. Run init-project first.";

    private static LoadRunsContext Context()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Sandbox, Mock.Of<ISandbox>());
        return new LoadRunsContext(
            new Repository(new BranchName("main"), "https://github.com/test/repo"), 10, pipeline);
    }

    private static LoadRunsHandler Handler(MapFileReaderFactory readers) =>
        new(readers, NullLogger<LoadRunsHandler>.Instance);

    [Fact]
    public async Task PriorRun_BootstrapAborted_NotIngested()
    {
        var readers = new MapFileReaderFactory(
            listings: new() { [RunsDir] = [$"{RunsDir}/2026-07-19T10-00-00-aaaa-fix-x", $"{RunsDir}/2026-07-19T11-00-00-bbbb-fix-y"] },
            files: new()
            {
                [$"{RunsDir}/2026-07-19T10-00-00-aaaa-fix-x/result.md"] = HealthyResult,
                [$"{RunsDir}/2026-07-19T11-00-00-bbbb-fix-y/result.md"] = BootstrapAbortedResult,
            });
        var context = Context();

        var result = await Handler(readers).ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("1 recent run");
        var history = context.Pipeline.Get<string>(ContextKeys.RunHistory);
        history.Should().Contain("retry policy");
        history.Should().NotContain("missing bootstrap");
    }

    [Fact]
    public async Task PriorRun_HealthyFailedRun_StillIngested()
    {
        // The gate is specific to the bootstrap abort — an ordinary failed run
        // is legitimate history (it carries real findings).
        var failed = "---\nresult: failed\n---\n\nTests stayed red after the fix attempt.";
        var readers = new MapFileReaderFactory(
            listings: new() { [RunsDir] = [$"{RunsDir}/2026-07-19T10-00-00-aaaa-fix-x"] },
            files: new() { [$"{RunsDir}/2026-07-19T10-00-00-aaaa-fix-x/result.md"] = failed });
        var context = Context();

        var result = await Handler(readers).ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.Pipeline.Get<string>(ContextKeys.RunHistory).Should().Contain("stayed red");
    }

    private sealed class MapFileReaderFactory(
        Dictionary<string, IReadOnlyList<string>> listings,
        Dictionary<string, string> files) : ISandboxFileReaderFactory
    {
        public ISandboxFileReader Create(ISandbox sandbox) => new MapReader(listings, files);
    }

    private sealed class MapReader(
        Dictionary<string, IReadOnlyList<string>> listings,
        Dictionary<string, string> files) : ISandboxFileReader
    {
        public Task<bool> ExistsAsync(string path, CancellationToken ct) =>
            Task.FromResult(files.ContainsKey(path));

        public Task<string?> TryReadAsync(string path, CancellationToken ct) =>
            Task.FromResult(files.TryGetValue(path, out var content) ? content : null);

        public Task<string> ReadRequiredAsync(string path, CancellationToken ct) =>
            Task.FromResult(files[path]);

        public Task WriteAsync(string path, string content, CancellationToken ct) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<string>> ListAsync(string path, int? maxDepth, CancellationToken ct) =>
            Task.FromResult(listings.TryGetValue(path, out var entries) ? entries : []);
    }
}
