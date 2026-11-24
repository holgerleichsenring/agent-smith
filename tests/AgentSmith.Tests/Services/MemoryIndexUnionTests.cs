using AgentSmith.Application.Services.Memory;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

/// <summary>2026-11-24-6811b: MEMORY.md changed on both sides of a base merge is united by entry name, the label taken
/// from the side that changed it; two different changes of one entry, or of the header, are no union.</summary>
public sealed class MemoryIndexUnionTests
{
    private const string Header = "# Memory index\n\n";
    private const string A = "- [a](a.md) (feedback, holding) — first";
    private const string B = "- [b](b.md) (project) — the branch's";
    private const string C = "- [c](c.md) (project) — the base's";

    [Fact]
    public void Resolve_BothSidesAppended_UnitesOursThenTheirs()
    {
        MemoryIndexUnion.Resolve(Header + A + "\n", Header + A + "\n" + B + "\n", Header + A + "\n" + C + "\n")
            .Should().Be(Header + A + "\n" + B + "\n" + C + "\n");
    }

    [Fact]
    public void Resolve_OneSideRelabels_TheOtherAppends_Unites()
    {
        var relabelled = A.Replace("holding", "unconfirmed");

        MemoryIndexUnion.Resolve(Header + A + "\n", Header + relabelled + "\n", Header + A + "\n" + C + "\n")
            .Should().Be(Header + relabelled + "\n" + C + "\n");
    }

    [Fact]
    public void Resolve_OneSideChangesTheDescription_TheOtherTheLabel_TakesBoth()
    {
        var described = A.Replace("first", "first, sharper");
        var relabelled = A.Replace("holding", "disputed");

        MemoryIndexUnion.Resolve(Header + A + "\n", Header + described + "\n", Header + relabelled + "\n")
            .Should().Be(Header + "- [a](a.md) (feedback, disputed) — first, sharper\n");
    }

    [Fact]
    public void Resolve_AnEntryRemovedOnOneSide_StaysRemoved()
    {
        MemoryIndexUnion.Resolve(Header + A + "\n" + B + "\n", Header + B + "\n", Header + A + "\n" + B + "\n" + C + "\n")
            .Should().Be(Header + B + "\n" + C + "\n");
    }

    [Fact]
    public void Resolve_TheSameEntryAddedTwice_IsKeptOnce_AddAddWithoutABase()
    {
        MemoryIndexUnion.Resolve(null, Header + B + "\n", Header + B + "\n" + C + "\n").Should().Be(Header + B + "\n" + C + "\n");
    }

    [Fact]
    public void Resolve_NoUnion_WhenOneEntryChangedDifferently_TheHeaderOnBoth_AStrayLine_OrASideDeleted()
    {
        MemoryIndexUnion.Resolve(Header + A + "\n", Header + A.Replace("first", "x") + "\n", Header + A.Replace("first", "y") + "\n")
            .Should().BeNull();
        MemoryIndexUnion.Resolve(Header + A + "\n", "# Mine\n\n" + A + "\n", "# Theirs\n\n" + A + "\n").Should().BeNull();
        MemoryIndexUnion.Resolve(Header + A + "\n", Header + A + "\nsome prose\n", Header + A + "\n" + C + "\n").Should().BeNull();
        MemoryIndexUnion.Resolve(Header + A + "\n", null, Header + A + "\n").Should().BeNull();
    }
}
