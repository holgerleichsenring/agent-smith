using AgentSmith.Application.Services.Specs.Review;
using FluentAssertions;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>2026-10-13-8762n: the splice rewrites one token inside one fact's evidence scalar and nothing else.</summary>
public sealed class SpecFactRegroundingTests
{
    private const string Yaml = """
        # yaml-language-server: $schema=../../spec.schema.json
        spec: 2026-10-14-1111a
        goal: "Keep src/Orders.cs:12 working"   # the goal names the token too
        facts:
          - claim: "The rules live in one class."
            evidence: "src/Orders.cs:12, src/Orders.cs:120-125 and src/Orders.cs:12-14"
          - claim: "Plain scalar evidence."
            evidence: src/Orders.cs:12
        steps:
          - id: one
            action: "Touch src/Orders.cs:12"
        """;

    [Fact]
    public void SpecFactRegrounding_ReplacesOnlyThatToken_RestOfFileByteIdentical()
    {
        var revised = SpecFactRegrounding.Apply(Yaml, 0, "src/Orders.cs:12", "src/Orders.cs:15");

        revised.Should().Be(Yaml.Replace(
            "\"src/Orders.cs:12, src/Orders.cs:120-125", "\"src/Orders.cs:15, src/Orders.cs:120-125"));
    }

    [Fact]
    public void SpecFactRegrounding_EscapedScalar_ReturnsNull()
    {
        const string escaped = "spec: x\nfacts:\n  - claim: \"c\"\n    evidence: \"src/Orders\\u002Ecs:12\"\n";

        SpecFactRegrounding.Apply(escaped, 0, "src/Orders.cs:12", "src/Orders.cs:15").Should().BeNull(
            "the token is not verbatim in the scalar's source");
    }

    [Fact]
    public void SpecFactRegrounding_RangeToken_KeepsTheRange()
    {
        var revised = SpecFactRegrounding.Apply(Yaml, 0, "src/Orders.cs:12-14", "src/Rules.cs:30-32");

        revised.Should().Contain("src/Orders.cs:12, src/Orders.cs:120-125 and src/Rules.cs:30-32\"")
            .And.Contain("evidence: src/Orders.cs:12\n", "the other fact is untouched");
    }

    [Fact]
    public void SpecFactRegrounding_TokenInTwoFactsOneEscaped_NoneReplaced()
    {
        const string yaml = "spec: x\nfacts:\n  - claim: \"a\"\n    evidence: \"src/Orders.cs:12\"\n"
            + "  - claim: \"b\"\n    evidence: \"see src/Orders\\u002Ecs:12\"\n";

        SpecFactRegrounding.ApplyAll(yaml, [0, 1], "src/Orders.cs:12", "src/Orders.cs:15").Should().BeNull(
            "a token moves in every fact that carries it, or in none");
        SpecFactRegrounding.ApplyAll(yaml, [0], "src/Orders.cs:12", "src/Orders.cs:15").Should().Contain("src/Orders.cs:15");
    }
}
