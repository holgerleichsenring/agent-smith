using AgentSmith.Contracts.Models.Structure;
using AgentSmith.Infrastructure.Services.Structure;
using FluentAssertions;

namespace AgentSmith.Tests.Structure;

/// <summary>
/// 2026-11-24-8756: a unit's count includes the line breaks a formatter would add, so joining
/// statements onto one line does not shrink it, and code with one statement per line counts its
/// physical lines as before.
/// </summary>
public sealed class StructureLayoutTests
{
    private const string Crammed = """
        public sealed class C
        {
            public int Get(int b) { var c = b + 1; if (c > 3) { return c * 2; } return c; }
        }
        """;

    // The same statements, one per line; a block with one statement may stay on its line.
    private const string Formatted = """
        public sealed class C
        {
            public int Get(int b) {
                var c = b + 1;
                if (c > 3) { return c * 2; }
                return c;
            }
        }
        """;

    private const string KAndR = """
        public sealed class C
        {
            public int Get(int b) {
                var c = b + 1;
                if (c > 3) {
                    return c * 2;
                }
                return c;
            }
        }
        """;

    private const string Allman = """
        public sealed class C
        {
            public int Get(int b)
            {
                var c = b + 1;
                if (c > 3)
                {
                    return c * 2;
                }
                return c;
            }
        }
        """;

    [Fact]
    public void Measure_CrammedAndFormattedMethod_CountEqual()
    {
        var crammed = Method(Crammed);
        var formatted = Method(Formatted);

        crammed.PhysicalLines.Should().Be(1);
        crammed.Lines.Should().Be(formatted.Lines, "the same statements count the same however many share a line");
        formatted.Lines.Should().Be(5);
        formatted.Breaks.Should().Be(0);
        Method(KAndR).Lines.Should().Be(7, "an expanded block counts its physical lines");
    }

    [Fact]
    public void Measure_FormattedAllmanMethod_CountsPhysicalLines()
    {
        var unit = Method(Allman);

        unit.Breaks.Should().Be(0);
        unit.Lines.Should().Be(9);
    }

    [Fact]
    public void Measure_GuardClauseAndEmptyBlock_AddNothing()
    {
        const string source = """
            public sealed class C
            {
                public void Run(int x)
                {
                    if (x > 1) return;
                    try
                    {
                        Do(x);
                    }
                    catch { }
                    var y = new System.Collections.Generic.List<int> { 1, 2, 3 };
                    if (y is null) { throw new System.ArgumentException(); }
                    System.Threading.Tasks.Task.Run(() => { return 1; });
                    checked { x++; }
                    int K(int a) { return a; }
                    switch (x)
                    {
                        case 1: return;
                        default:
                            break;
                    }
                }
            }
            """;

        StructureEngineFixture.Engine().Measure("src/C.cs", source).Units.Single(u => u.Key == "C.Run(int)").Breaks
            .Should().Be(0, "an embedded statement, an empty or one-statement block and an initializer are formatted code");
    }

    [Fact]
    public void Measure_CrammedClassMembers_CountForTheType()
    {
        const string crammed = "public sealed class C { private int _a; public int B { get; set; } }\n";

        var type = StructureEngineFixture.Engine().Measure("src/C.cs", crammed)
            .Units.Single(u => u.Kind == StructureUnitKind.Type);

        type.Lines.Should().Be(4, "a formatter puts each member and the closing brace on its own line");
    }

    [Fact]
    public void Measure_RustCrammedFunction_CountsLikeRustfmt()
    {
        const string crammed = "fn get(b: i32) -> i32 { let c = b + 1; if c > 3 { return c * 2; } c }\n";
        const string formatted = """
            fn get(b: i32) -> i32 {
                let c = b + 1;
                if c > 3 { return c * 2; }
                c
            }

            """;

        var engine = StructureEngineFixture.Engine();
        var one = engine.Measure("src/lib.rs", crammed).Units.Single();
        var other = engine.Measure("src/lib.rs", formatted).Units.Single();

        one.Lines.Should().Be(other.Lines).And.Be(5);
        other.Breaks.Should().Be(0);
    }

    [Fact]
    public void Measure_RustfmtOneLineForms_AddNothing()
    {
        const string source = """
            fn run(x: bool, o: Option<i32>) -> i32 {
                let y = if x { 1 } else { 2 };
                let Some(v) = o else { return 0 };
                let z = unsafe { f() };
                let w = 'a: { break 'a 1 };
                y + v + z + w
            }

            """;

        StructureEngineFixture.Engine().Measure("src/lib.rs", source).Units.Single().Breaks.Should().Be(0);
    }

    [Theory]
    // Each count is the K&R layout's lines minus one; a section's label shares its first statement's line.
    [InlineData("void M() { switch (0) { default: a(); b(); c(); break; } }", 7)]
    [InlineData("void M() { if (x) { a(); b(); } }", 5)]
    [InlineData("void M() { if (x) { if (y) { a(); } } }", 4)]
    [InlineData("void M() { switch (x) { case 1: return 2; } }", 2)]
    public void Measure_WrappedStatements_StillCount(string method, int breaks)
    {
        var unit = StructureEngineFixture.Engine().Measure("src/C.cs", $"class C\n{{\n    {method}\n}}\n")
            .Units.Single(u => u.Kind == StructureUnitKind.Function);

        unit.Breaks.Should().Be(breaks, "wrapping statements in a block or a switch section does not hide them");
    }

    [Fact]
    public void Measure_RustMatchArmsOnOneLine_Count()
    {
        var unit = StructureEngineFixture.Engine().Measure("src/lib.rs", "fn f(x: i32) -> i32 { match x { 1 => 2, 2 => 3, _ => 4 } }\n")
            .Units.Single();

        unit.Breaks.Should().BeGreaterThanOrEqualTo(4, "rustfmt puts each arm on its own line");
    }

    [Fact]
    public void Breaks_LanguageWithoutLayout_CountsPhysicalLines()
    {
        var cs = new StructureLanguageCatalog().For("src/C.cs").Language!;
        var bare = new StructureLanguage
        {
            Name = cs.Name, Extensions = cs.Extensions, Library = cs.Library, Symbol = cs.Symbol,
            AttributeKinds = cs.AttributeKinds, Containers = cs.Containers, Exclude = cs.Exclude, Query = cs.Query,
        };

        var unit = StructureEngineFixture.Engine(new StructureLanguageCatalog([bare])).Measure("src/C.cs", Crammed)
            .Units.Single(u => u.Kind == StructureUnitKind.Function);

        unit.Breaks.Should().Be(0);
        unit.Lines.Should().Be(1);
    }

    private static StructureUnit Method(string source) =>
        StructureEngineFixture.Engine().Measure("src/C.cs", source).Units.Single(u => u.Kind == StructureUnitKind.Function);
}
