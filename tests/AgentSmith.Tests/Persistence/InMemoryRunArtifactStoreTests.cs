using AgentSmith.Application.Services.Persistence;
using FluentAssertions;

namespace AgentSmith.Tests.Persistence;

public sealed class InMemoryRunArtifactStoreTests
{
    [Fact]
    public async Task WritePlanAsync_ReadPlanAsync_RoundTrip()
    {
        var store = new InMemoryRunArtifactStore();

        await store.WritePlanAsync("r01", "{\"plan\":1}", CancellationToken.None);
        var read = await store.ReadPlanAsync("r01", CancellationToken.None);

        read.Should().Be("{\"plan\":1}");
    }

    [Fact]
    public async Task PromoteAsync_ReturnsAllSlots()
    {
        var store = new InMemoryRunArtifactStore();
        await store.WritePlanAsync("r02", "p", CancellationToken.None);
        await store.WriteDiffAsync("r02", "d", CancellationToken.None);
        await store.WriteBootstrapAsync("r02", "b", CancellationToken.None);

        var snapshot = await store.PromoteAsync("r02", CancellationToken.None);

        snapshot.PlanJson.Should().Be("p");
        snapshot.DiffJson.Should().Be("d");
        snapshot.BootstrapMarkdown.Should().Be("b");
        snapshot.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public async Task ClearAsync_RemovesAllKeys()
    {
        var store = new InMemoryRunArtifactStore();
        await store.WritePlanAsync("r03", "p", CancellationToken.None);
        await store.WriteDiffAsync("r03", "d", CancellationToken.None);

        await store.ClearAsync("r03", CancellationToken.None);

        (await store.ReadPlanAsync("r03", CancellationToken.None)).Should().BeNull();
        (await store.ReadDiffAsync("r03", CancellationToken.None)).Should().BeNull();
        (await store.PromoteAsync("r03", CancellationToken.None)).IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task TtlElapsed_ReadReturnsNull()
    {
        var time = DateTimeOffset.UtcNow;
        var store = new InMemoryRunArtifactStore(TimeSpan.FromMinutes(1), () => time);
        await store.WritePlanAsync("r04", "p", CancellationToken.None);

        time = time.AddMinutes(2);

        var read = await store.ReadPlanAsync("r04", CancellationToken.None);
        read.Should().BeNull();
    }

    [Fact]
    public async Task PromoteAsync_NoData_ReturnsEmpty()
    {
        var store = new InMemoryRunArtifactStore();

        var snapshot = await store.PromoteAsync("nope", CancellationToken.None);

        snapshot.IsEmpty.Should().BeTrue();
    }
}
