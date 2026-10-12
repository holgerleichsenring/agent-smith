using AgentSmith.Contracts.Models.Structure;
using FluentAssertions;

namespace AgentSmith.Tests.Structure;

public sealed class TreeSitterStructureEngineCSharpTests
{
    private const string TwoMethods = """
        namespace N;

        public sealed class Handler
        {
            public void Run(int count, string name)
            {
                var x = count;
            }

            public void Stop() { }
        }
        """;

    [Fact]
    public void Measure_CSharpClassWithTwoMethods_ReturnsTypeAndFunctionsWithSpans()
    {
        var result = StructureEngineFixture.Engine().Measure("src/Handler.cs", TwoMethods);

        result.Outcome.Should().Be(StructureOutcome.Measured);
        result.Units.Should().BeEquivalentTo(new[]
        {
            new StructureUnit(StructureUnitKind.Type, "Handler", 3, 11),
            new StructureUnit(StructureUnitKind.Function, "Handler.Run(int, string)", 5, 8),
            new StructureUnit(StructureUnitKind.Function, "Handler.Stop()", 10, 10),
        });
        result.TypeCount.Should().Be(1);
        result.FileLines.Should().Be(11);
    }

    [Fact]
    public void Measure_CSharpOverloadInsertedAbove_KeepsTheOtherKey()
    {
        const string before = "class A\n{\n    void M(int x) { }\n}\n";
        const string after = "class A\n{\n    void M(string s) { }\n    void M(int x) { }\n}\n";
        var engine = StructureEngineFixture.Engine();

        var keysBefore = engine.Measure("A.cs", before).Units.Select(u => u.Key);
        var keysAfter = engine.Measure("A.cs", after).Units.Select(u => u.Key);

        keysAfter.Should().Contain(keysBefore).And.Contain("A.M(string)");
    }

    [Fact]
    public void Measure_CSharpAttributeLines_AreNotCounted()
    {
        const string source = "class A\n{\n    [Fact]\n    [Trait(\"a\", \"b\")]\n    void M()\n    {\n    }\n}\n";

        var method = StructureEngineFixture.Engine().Measure("A.cs", source).Units
            .Single(u => u.Kind == StructureUnitKind.Function);

        method.StartLine.Should().Be(5);
        method.Lines.Should().Be(3);
    }

    [Fact]
    public void Measure_CSharpLocalFunction_IsAUnitInsideItsParent()
    {
        const string source =
            "class A\n{\n    void M()\n    {\n        int Local() => 1;\n        var f = () => 2;\n    }\n}\n";

        var units = StructureEngineFixture.Engine().Measure("A.cs", source).Units;

        units.Select(u => u.Key).Should().BeEquivalentTo("A", "A.M()", "A.M().Local()");
        units.Single(u => u.Key == "A.M()").Lines.Should().Be(5, "the local function counts inside it");
    }

    [Fact]
    public void Measure_CSharpNestedType_CountsOneTopLevelType()
    {
        const string source = "class A\n{\n    class B { }\n}\nrecord C(int X);\n";

        StructureEngineFixture.Engine().Measure("A.cs", source).TypeCount.Should().Be(2);
    }

    [Theory]
    [InlineData("tests/AgentSmith.Tests/HandlerTests.cs")]
    [InlineData("src/Api.Tests/HandlerTests.cs")]
    [InlineData("src/Api/Migrations/20260101_Init.cs")]
    [InlineData("src/Api/Model.g.cs")]
    public void Measure_CSharpTestProjectPath_ReturnsNotMeasured(string path)
    {
        StructureEngineFixture.Engine().Measure(path, TwoMethods).Outcome
            .Should().Be(StructureOutcome.NotMeasured);
    }

    [Fact]
    public void Measure_CSharpWordEndingInTest_IsMeasured()
    {
        StructureEngineFixture.Engine().Measure("src/Contest/Handler.cs", TwoMethods).Outcome
            .Should().Be(StructureOutcome.Measured);
    }

    [Fact]
    public void Measure_CSharpOperatorsPropertiesAndIndexers_AreFunctions()
    {
        const string source = "class A\n{\n    public static A operator +(A a, A b) => a;\n"
            + "    public int P\n    {\n        get { return 1; }\n    }\n    public int this[int i] => i;\n}\n";

        var keys = StructureEngineFixture.Engine().Measure("A.cs", source).Units
            .Where(u => u.Kind == StructureUnitKind.Function).Select(u => u.Key);

        keys.Should().BeEquivalentTo("A.+(A, A)", "A.P", "A.this(int)");
    }

    [Fact]
    public void Measure_CSharpCommentBetweenAttributeAndMethod_IsNotCounted()
    {
        const string source = "class A\n{\n    [Fact]\n    // why\n    void M()\n    {\n    }\n}\n";

        StructureEngineFixture.Engine().Measure("A.cs", source).Units
            .Single(u => u.Kind == StructureUnitKind.Function).StartLine.Should().Be(5);
    }
}
