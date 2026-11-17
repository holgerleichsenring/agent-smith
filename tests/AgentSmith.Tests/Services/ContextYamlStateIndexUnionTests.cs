using AgentSmith.Infrastructure.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

/// <summary>2026-11-17-023fb: the union of two context.yaml files that each added state.done entries.</summary>
public sealed class ContextYamlStateIndexUnionTests
{
    private const string Base = "meta: { name: demo }\nstate:\n  done:\n    p1: \"one\"\n  active: {}\n";
    private static readonly ContextYamlStateIndexUnion Union = new();

    private static string With(string text, string entry) => text.Replace("  done:\n", $"  done:\n    {entry}\n");

    [Fact]
    public void BothAdded_KeepsBoth()
    {
        var result = Union.Resolve(Base, With(Base, "a: \"ours\""), With(Base, "b: \"theirs\""));

        result.Should().Contain("a: \"ours\"").And.Contain("b: \"theirs\"").And.Contain("p1: \"one\"");
    }

    [Fact]
    public void SameKeySameText_IsDeduplicated()
    {
        var result = Union.Resolve(Base, With(Base, "a: \"same\""), With(Base, "a: \"same\""));

        result!.Split("a: \"same\"").Length.Should().Be(2);
    }

    [Fact]
    public void SameKeyDifferentText_IsNoUnion() =>
        Union.Resolve(Base, With(Base, "a: \"x\""), With(Base, "a: \"y\"")).Should().BeNull();

    [Fact]
    public void AChangedBaseEntry_IsNoUnion() =>
        Union.Resolve(Base, Base.Replace("\"one\"", "\"uno\""), With(Base, "b: \"theirs\"")).Should().BeNull();

    [Fact]
    public void BothChangedOutsideDone_IsNoUnion() =>
        Union.Resolve(Base, With(Base, "a: \"x\"").Replace("demo", "left"), With(Base, "b: \"y\"").Replace("demo", "right"))
            .Should().BeNull();

    [Fact]
    public void OneSideChangedOutsideDone_KeepsThatChange()
    {
        var result = Union.Resolve(Base, With(Base, "a: \"x\""), With(Base, "b: \"y\"").Replace("demo", "renamed"));

        result.Should().Contain("renamed").And.Contain("a: \"x\"").And.Contain("b: \"y\"");
    }

    [Fact]
    public void AddedOnBothSides_WithoutABase_UnitesWhenEqualOutsideDone()
    {
        var seeded = "meta: workdir\nstate:\n  done:\n  active: {}\n";

        Union.Resolve(null, With(seeded, "a: \"x\""), With(seeded, "b: \"y\""))
            .Should().Contain("a: \"x\"").And.Contain("b: \"y\"");
    }

    [Fact]
    public void ADeletedSide_IsNoUnion() => Union.Resolve(Base, null, Base).Should().BeNull();
}
