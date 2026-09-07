using AgentSmith.Application.Services.Expectations;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Expectations;

/// <summary>
/// 2026-09-07-24ed: an expected line that quotes a code shape carries braces. The old
/// brace count never saw the draft close and the drafter retried a correct answer.
/// </summary>
public sealed class ExpectationDraftParserTests
{
    [Fact]
    public void TryParse_ExpectedLineContainsBraces_StillParses()
    {
        var text = """
            Here is the draft:
            { "observed": "the config has no rootDir",
              "expected": ["tsconfig.build.json carries { \"rootDir\": \"./source\" }"],
              "constraints": [] }
            """;

        var draft = ExpectationDraftParser.TryParse(text);

        draft.Should().NotBeNull();
        draft!.Expected.Should().ContainSingle().Which.Should().Contain("{ \"rootDir\"");
    }

    [Fact]
    public void TryParse_ExpectedLineContainsAnUnbalancedBrace_StillParses()
    {
        var text = """
            { "observed": "a block opens with { and the build fails",
              "expected": ["the block closes"], "constraints": ["no { in prose"] }
            """;

        var draft = ExpectationDraftParser.TryParse(text);

        draft.Should().NotBeNull();
        draft!.Observed.Should().Contain("opens with {");
        draft.Constraints.Should().ContainSingle();
    }
}
