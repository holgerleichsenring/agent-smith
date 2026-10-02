using AgentSmith.Application.Services.Persistence;
using FluentAssertions;

namespace AgentSmith.Tests.Persistence;

public sealed class InMemoryRunArtifactStoreTests
{
    [Fact]
    public async Task WriteResultMarkdownAsync_ReadResultMarkdownAsync_RoundTrip()
    {
        var store = new InMemoryRunArtifactStore();

        await store.WriteResultMarkdownAsync("r01", "# Result", CancellationToken.None);
        var read = await store.ReadResultMarkdownAsync("r01", CancellationToken.None);

        read.Should().Be("# Result");
    }

    [Fact]
    public async Task Slots_AreIndependent()
    {
        var store = new InMemoryRunArtifactStore();
        await store.WritePlanMarkdownAsync("r02", "p", CancellationToken.None);
        await store.WriteAnalyzeMarkdownAsync("r02", "a", CancellationToken.None);

        (await store.ReadPlanMarkdownAsync("r02", CancellationToken.None)).Should().Be("p");
        (await store.ReadAnalyzeMarkdownAsync("r02", CancellationToken.None)).Should().Be("a");
        (await store.ReadSpecMarkdownAsync("r02", CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task TtlElapsed_ReadReturnsNull()
    {
        var time = DateTimeOffset.UtcNow;
        var store = new InMemoryRunArtifactStore(TimeSpan.FromMinutes(1), () => time);
        await store.WritePlanMarkdownAsync("r04", "p", CancellationToken.None);

        time = time.AddMinutes(2);

        var read = await store.ReadPlanMarkdownAsync("r04", CancellationToken.None);
        read.Should().BeNull();
    }
}
