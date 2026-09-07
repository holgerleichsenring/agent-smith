using AgentSmith.Application.Services;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

/// <summary>
/// 2026-09-07-24ed: the verdict's evidence quotes code, and code has braces. The old
/// brace count read a "{" inside the evidence string as structure, never saw the verdict
/// close, and the keystone treated a green run as unverified.
/// </summary>
public sealed class MasterVerificationParserBracesInStringsTests
{
    [Fact]
    public void TryParse_EvidenceContainsBraces_StillParses()
    {
        var text = """
            Verified. { "status": "green", "build_ran": true, "build_passed": true,
              "tests_ran": true, "tests_passed": true,
              "acceptance": [ { "criterion": "C", "status": "met", "evidence": "added { \"extends\": \"./tsconfig.json\" }" } ] }
            """;

        var v = MasterVerificationParser.TryParse(text);

        v.Should().NotBeNull();
        v!.Status.Should().Be(VerificationStatus.Green);
        v.AcceptanceDispositions![0].Evidence.Should().Contain("{ \"extends\"");
    }

    [Fact]
    public void TryParse_EvidenceContainsAnUnbalancedBrace_StillParses()
    {
        var text = """
            { "status": "failed", "tests_ran": true, "tests_passed": false,
              "acceptance": [ { "criterion": "C", "status": "unmet", "evidence": "the block opened with { and never closed" } ] }
            """;

        var v = MasterVerificationParser.TryParse(text);

        v.Should().NotBeNull();
        v!.Status.Should().Be(VerificationStatus.Failed);
        v.AcceptanceDispositions![0].Evidence.Should().Contain("opened with {");
    }
}
