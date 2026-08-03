using AgentSmith.Application.Services.Persistence;
using AgentSmith.Server.Services.Events;
using FluentAssertions;

namespace AgentSmith.Tests.Hubs;

/// <summary>
/// p0395: runs cached before the "Definition of done" rendering carry the raw
/// yaml key as a literal "- done: " list prefix in their stored copy. The read
/// path strips it, so old runs render clean without their artifacts being
/// rewritten; already-clean copies pass through untouched.
/// </summary>
public sealed class SpecMarkdownReaderTests
{
    private const string ValidRunId = "2026-08-03T12-34-56-abcd";

    [Fact]
    public async Task ReadAsync_LegacyDonePrefixedCopy_IsServedWithoutThePrefix()
    {
        var store = new InMemoryRunArtifactStore();
        await store.WriteSpecMarkdownAsync(
            ValidRunId,
            "## Phases\n- **p19106a** — Rename\n  - done: Every call site is renamed.\n",
            CancellationToken.None);
        var reader = new SpecMarkdownReader(store);

        var content = await reader.ReadAsync(ValidRunId, CancellationToken.None);

        content.Should().Contain("  - Every call site is renamed.");
        content.Should().NotContain("done:");
    }

    [Fact]
    public async Task ReadAsync_CleanCopy_PassesThroughUnchanged()
    {
        var clean = "## Phases\n- **p19106a** — Rename\n  - Definition of done:\n    - Done.\n";
        var store = new InMemoryRunArtifactStore();
        await store.WriteSpecMarkdownAsync(ValidRunId, clean, CancellationToken.None);
        var reader = new SpecMarkdownReader(store);

        var content = await reader.ReadAsync(ValidRunId, CancellationToken.None);

        content.Should().Be(clean);
    }

    [Fact]
    public async Task ReadAsync_StoreReturnsNull_ReturnsNull()
    {
        var reader = new SpecMarkdownReader(new InMemoryRunArtifactStore());

        var content = await reader.ReadAsync(ValidRunId, CancellationToken.None);

        content.Should().BeNull();
    }
}
