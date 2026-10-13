using AgentSmith.Contracts.Models.Structure;
using AgentSmith.Infrastructure.Services.Structure;
using FluentAssertions;

namespace AgentSmith.Tests.Structure;

public sealed class TreeSitterStructureEngineRustTests
{
    [Fact]
    public void Measure_RustCfgTestModule_ExcludesItsFunctions()
    {
        const string source = """
            fn real() {}

            #[cfg(test)]
            mod tests {
                #[test]
                fn checks() {}
            }

            #[cfg(feature = "x")]
            mod gated { fn kept() {} }
            """;

        var keys = StructureEngineFixture.Engine().Measure("src/lib.rs", source).Units.Select(u => u.Key);

        keys.Should().BeEquivalentTo("real()", "gated.kept()");
    }

    [Fact]
    public void Measure_RustTwoTraitImplsSameFn_KeysIndependentOfOrder()
    {
        const string display = "impl Display for Foo {\n    fn fmt(&self) {}\n}\n";
        const string debug = "impl Debug for Foo {\n    fn fmt(&self) {}\n}\n";
        var engine = StructureEngineFixture.Engine();

        var one = engine.Measure("src/foo.rs", display + debug).Units.Select(u => u.Key);
        var other = engine.Measure("src/foo.rs", debug + display).Units.Select(u => u.Key);

        one.Should().BeEquivalentTo("Display for Foo.fmt(&self)", "Debug for Foo.fmt(&self)");
        other.Should().BeEquivalentTo(one);
    }

    [Fact]
    public void Measure_RustNestedFn_IsAUnitInsideItsParent()
    {
        const string source = "impl Foo {\n    pub fn new() -> Self {\n        fn helper() {}\n        Foo\n    }\n}\n";

        var units = StructureEngineFixture.Engine().Measure("src/foo.rs", source).Units;

        units.Select(u => u.Key).Should().BeEquivalentTo("Foo.new()", "Foo.new().helper()");
        units.Single(u => u.Key == "Foo.new()").Lines.Should().Be(4);
    }

    [Fact]
    public void Measure_RustIntegrationTestsDirectory_ReturnsNotMeasured()
    {
        StructureEngineFixture.Engine().Measure("tests/api.rs", "fn t() {}").Outcome
            .Should().Be(StructureOutcome.NotMeasured);
    }

    [Fact]
    public void Measure_UnknownExtension_ReturnsNotMeasured()
    {
        StructureEngineFixture.Engine().Measure("README.md", "# hi").Outcome
            .Should().Be(StructureOutcome.NotMeasured);
    }

    [Fact]
    public void Measure_GrammarFailsToLoad_ReturnsUnavailable()
    {
        var missing = new StructureLanguage
        {
            Name = "missing", Extensions = [".zz"], Library = "tree-sitter-does-not-exist",
            Symbol = "tree_sitter_does_not_exist", Query = "(x) @type",
        };
        var engine = StructureEngineFixture.Engine(new StructureLanguageCatalog([missing]));

        var result = engine.Measure("a.zz", "anything");

        result.Outcome.Should().Be(StructureOutcome.Unavailable);
        result.Reason.Should().Contain("tree-sitter-does-not-exist");
    }

    [Fact]
    public void Measure_RustSingleItemWithoutNewline_KeysAndCountsItOnce()
    {
        var result = StructureEngineFixture.Engine().Measure("src/lib.rs", "struct A;");

        result.Units.Should().ContainSingle().Which.Key.Should().Be("A");
        result.TypeCount.Should().Be(1);
    }
}
