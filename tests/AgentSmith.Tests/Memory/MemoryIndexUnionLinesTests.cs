using AgentSmith.Application.Services.Memory;
using FluentAssertions;

namespace AgentSmith.Tests.Memory;

/// <summary>
/// 2026-11-26-462aa: MEMORY.md merges with merge=union, which keeps both versions of a line two branches changed and
/// brings back a line one side deleted. The writers keep the first line per name; the relabel drops a line whose file
/// is gone; the composer shows one line per name.
/// </summary>
public sealed class MemoryIndexUnionLinesTests
{
    private const string Unioned = "# Memory index\n\n- [a](a.md) (feedback, holding) — first\n- [b](b.md) (project) — b\n- [a](a.md) (feedback, unconfirmed) — first\n";

    [Fact]
    public void Relabel_TwoLinesForOneName_LeavesTheFirst()
    {
        var text = new MemoryIndexLabels().Relabel(Unioned, new Dictionary<string, MemoryStanding>());

        text.Split('\n').Count(l => l.StartsWith("- [a]", StringComparison.Ordinal)).Should().Be(1);
        text.Should().Contain("- [b](b.md)");
    }

    [Fact]
    public void Relabel_ALineWhoseFileIsGone_IsDropped()
    {
        var text = new MemoryIndexLabels().Relabel(Unioned, new Dictionary<string, MemoryStanding>(),
            new HashSet<string>(StringComparer.Ordinal) { "MEMORY.md", "b.md" });

        text.Should().NotContain("- [a]").And.Contain("- [b](b.md)");
    }

    [Fact]
    public void Relabel_AnEmptyListing_DropsNothing()
    {
        var text = new MemoryIndexLabels().Relabel(Unioned, new Dictionary<string, MemoryStanding>(), new HashSet<string>());

        text.Should().Contain("- [a]").And.Contain("- [b]");
    }

    [Fact]
    public void Upsert_ANameWithATwin_LeavesOneLine()
    {
        var text = MemoryIndexUpdater.Upsert(Unioned,
            new MemoryEntry("a", "first, updated", MemoryEntryType.Feedback, "body"));

        text.Split('\n').Where(l => l.StartsWith("- [a]", StringComparison.Ordinal)).Should().ContainSingle()
            .Which.Should().Contain("first, updated");
    }

    [Fact]
    public void Compose_TwoLinesForOneName_ShowsOne()
    {
        var shown = new MemoryIndexComposer().Compose(Unioned, new Dictionary<string, MemoryStanding>());

        shown.Split('\n').Count(l => l.StartsWith("- [a]", StringComparison.Ordinal)).Should().Be(1);
    }
}
