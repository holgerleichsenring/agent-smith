using AgentSmith.Application.Services.Persistence;
using AgentSmith.Server.Services.Events;
using FluentAssertions;

namespace AgentSmith.Tests.Hubs;

public sealed class ResultMarkdownReaderTests
{
    private const string ValidRunId = "2026-05-27T12-34-56-abcd";

    [Fact]
    public async Task ReadAsync_KnownRunWithResult_ReturnsContent()
    {
        var store = new InMemoryRunArtifactStore();
        await store.WriteResultMarkdownAsync(ValidRunId, "# Run Result\n\nbody.", CancellationToken.None);
        var reader = new ResultMarkdownReader(store);

        var content = await reader.ReadAsync(ValidRunId, CancellationToken.None);

        content.Should().NotBeNull();
        content.Should().Contain("# Run Result");
    }

    [Fact]
    public async Task ReadAsync_StoreReturnsNull_ReturnsNull()
    {
        var store = new InMemoryRunArtifactStore();
        var reader = new ResultMarkdownReader(store);

        var content = await reader.ReadAsync(ValidRunId, CancellationToken.None);

        content.Should().BeNull();
    }

    [Theory]
    [InlineData("../etc/passwd")]
    [InlineData("../../escape")]
    [InlineData("r01-old-format-rejected")]
    [InlineData("")]
    [InlineData("not-a-real-runid")]
    public async Task ReadAsync_InvalidRunId_ReturnsNullWithoutHittingStore(string runId)
    {
        var store = new ThrowingStore(); // any store call would throw
        var reader = new ResultMarkdownReader(store);

        var content = await reader.ReadAsync(runId, CancellationToken.None);

        content.Should().BeNull();
    }

    private sealed class ThrowingStore : AgentSmith.Contracts.Persistence.IRunArtifactStore
    {
        public Task WriteResultMarkdownAsync(string runId, string resultMd, CancellationToken ct) => throw new InvalidOperationException("should not be called");
        public Task<string?> ReadResultMarkdownAsync(string runId, CancellationToken ct) => throw new InvalidOperationException("should not be called");
        public Task WritePlanMarkdownAsync(string runId, string planMd, CancellationToken ct) => throw new InvalidOperationException("should not be called");
        public Task<string?> ReadPlanMarkdownAsync(string runId, CancellationToken ct) => throw new InvalidOperationException("should not be called");
        public Task WriteSpecMarkdownAsync(string runId, string specMd, CancellationToken ct) => throw new InvalidOperationException("should not be called");
        public Task<string?> ReadSpecMarkdownAsync(string runId, CancellationToken ct) => throw new InvalidOperationException("should not be called");
        public Task WriteAnalyzeMarkdownAsync(string runId, string analyzeMd, CancellationToken ct) => throw new InvalidOperationException("should not be called");
        public Task<string?> ReadAnalyzeMarkdownAsync(string runId, CancellationToken ct) => throw new InvalidOperationException("should not be called");
    }
}
