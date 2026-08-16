using AgentSmith.Application.Services.Preflight.Run;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services.Preflight.Run;

/// <summary>
/// p0428: "Read-only file system : '/root'" killed two live runs at step 6. The probe
/// proves the home accepts a write, and says HOME rather than quoting the exception.
/// </summary>
public sealed class SandboxHomeWritableCheckTests
{
    [Fact]
    public async Task AReadOnlyHome_NamesHomeNotTheException()
    {
        var check = new SandboxHomeWritableCheck(
            new ReadOnlyHomeFactory(), NullLogger<SandboxHomeWritableCheck>.Instance);

        var finding = await check.RunAsync(PipelineWithOneSandbox(), CancellationToken.None);

        finding.Verdict.Should().Be(RunPreflightVerdict.Fail);
        finding.Message.Should().Contain("/root").And.Contain("api");
        finding.Message.Should().NotContain("UnauthorizedAccessException");
        finding.Lever.Should().Contain("writable");
    }

    [Fact]
    public async Task AWritableHome_Passes()
    {
        var check = new SandboxHomeWritableCheck(
            new WritableHomeFactory(), NullLogger<SandboxHomeWritableCheck>.Instance);

        var finding = await check.RunAsync(PipelineWithOneSandbox(), CancellationToken.None);

        finding.Verdict.Should().Be(RunPreflightVerdict.Pass);
    }

    [Fact]
    public async Task NoSandboxes_Passes()
    {
        var check = new SandboxHomeWritableCheck(
            new ReadOnlyHomeFactory(), NullLogger<SandboxHomeWritableCheck>.Instance);

        var finding = await check.RunAsync(new PipelineContext(), CancellationToken.None);

        finding.Verdict.Should().Be(RunPreflightVerdict.Pass);
    }

    private static PipelineContext PipelineWithOneSandbox()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox> { ["api"] = new ScriptedSandbox() });
        return pipeline;
    }

    private sealed class WritableHomeFactory : ISandboxFileReaderFactory
    {
        private readonly InMemorySandboxFileReader _reader = new();

        public ISandboxFileReader Create(ISandbox sandbox) => _reader;
    }

    private sealed class ReadOnlyHomeFactory : ISandboxFileReaderFactory
    {
        public ISandboxFileReader Create(ISandbox sandbox) => new ThrowingReader();

        private sealed class ThrowingReader : ISandboxFileReader
        {
            public Task<bool> ExistsAsync(string path, CancellationToken ct) => Task.FromResult(false);

            public Task<string?> TryReadAsync(string path, CancellationToken ct) =>
                Task.FromResult<string?>(null);

            public Task<string> ReadRequiredAsync(string path, CancellationToken ct) =>
                throw new FileNotFoundException(path);

            public Task WriteAsync(string path, string content, CancellationToken ct) =>
                throw new UnauthorizedAccessException($"Read-only file system : '{path}'");

            public Task<IReadOnlyList<string>> ListAsync(string path, int? maxDepth, CancellationToken ct) =>
                Task.FromResult<IReadOnlyList<string>>([]);
        }
    }
}
