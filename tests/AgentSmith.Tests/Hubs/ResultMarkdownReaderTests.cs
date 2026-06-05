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
        public Task WritePlanAsync(string runId, string planJson, CancellationToken ct) => throw new InvalidOperationException("should not be called");
        public Task<string?> ReadPlanAsync(string runId, CancellationToken ct) => throw new InvalidOperationException("should not be called");
        public Task WriteDiffAsync(string runId, string diffJson, CancellationToken ct) => throw new InvalidOperationException("should not be called");
        public Task<string?> ReadDiffAsync(string runId, CancellationToken ct) => throw new InvalidOperationException("should not be called");
        public Task WriteBootstrapAsync(string runId, string bootstrapMarkdown, CancellationToken ct) => throw new InvalidOperationException("should not be called");
        public Task<string?> ReadBootstrapAsync(string runId, CancellationToken ct) => throw new InvalidOperationException("should not be called");
        public Task WriteResultMarkdownAsync(string runId, string resultMd, CancellationToken ct) => throw new InvalidOperationException("should not be called");
        public Task<string?> ReadResultMarkdownAsync(string runId, CancellationToken ct) => throw new InvalidOperationException("should not be called");
        public Task WritePlanMarkdownAsync(string runId, string planMd, CancellationToken ct) => throw new InvalidOperationException("should not be called");
        public Task<string?> ReadPlanMarkdownAsync(string runId, CancellationToken ct) => throw new InvalidOperationException("should not be called");
        public Task<AgentSmith.Contracts.Persistence.RunArtifactSnapshot> PromoteAsync(string runId, CancellationToken ct) => throw new InvalidOperationException("should not be called");
        public Task ClearAsync(string runId, CancellationToken ct) => throw new InvalidOperationException("should not be called");
    }
}
