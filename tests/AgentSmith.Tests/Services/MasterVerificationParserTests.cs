using AgentSmith.Application.Services;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public sealed class MasterVerificationParserTests
{
    [Fact]
    public void FencedVerdictBlock_Green_Parsed()
    {
        var text = """
            All done. Here is my verdict:

            ```verdict
            { "status": "green", "build_ran": true, "build_passed": true, "tests_ran": true, "tests_passed": true, "summary": "fixed the controller" }
            ```
            """;

        var v = MasterVerificationParser.TryParse(text);

        v.Should().NotBeNull();
        v!.Status.Should().Be(VerificationStatus.Green);
        v.BuildPassed.Should().BeTrue();
        v.TestsPassed.Should().BeTrue();
        v.Summary.Should().Be("fixed the controller");
    }

    [Fact]
    public void BareJsonObject_Parsed()
    {
        var text = "I changed the file. {\"status\":\"failed\",\"tests_ran\":true,\"tests_passed\":false}";

        var v = MasterVerificationParser.TryParse(text);

        v.Should().NotBeNull();
        v!.Status.Should().Be(VerificationStatus.Failed);
        v.TestsRan.Should().BeTrue();
        v.TestsPassed.Should().BeFalse();
    }

    [Fact]
    public void CamelCaseKeys_Parsed()
    {
        var text = "```json\n{ \"status\": \"green\", \"buildRan\": true, \"buildPassed\": true }\n```";

        var v = MasterVerificationParser.TryParse(text);

        v.Should().NotBeNull();
        v!.BuildRan.Should().BeTrue();
        v.BuildPassed.Should().BeTrue();
    }

    [Fact]
    public void NoVerdict_ReturnsNull()
    {
        MasterVerificationParser.TryParse("I looked around but didn't write a verdict.").Should().BeNull();
        MasterVerificationParser.TryParse("").Should().BeNull();
        MasterVerificationParser.TryParse(null).Should().BeNull();
    }

    [Fact]
    public void NoTestsStatus_Parsed()
    {
        var v = MasterVerificationParser.TryParse("{\"status\":\"no-tests\"}");

        v.Should().NotBeNull();
        v!.Status.Should().Be(VerificationStatus.NoTests);
    }

    [Fact]
    public void LastVerdictWins()
    {
        var text = """
            ```verdict
            { "status": "failed" }
            ```
            wait, I fixed it:
            ```verdict
            { "status": "green", "tests_ran": true, "tests_passed": true }
            ```
            """;

        var v = MasterVerificationParser.TryParse(text);

        v.Should().NotBeNull();
        v!.Status.Should().Be(VerificationStatus.Green);
    }

    [Fact]
    public void ObjectWithoutStatus_Ignored()
    {
        // A JSON object that isn't a verdict (no status) must not be mistaken for one.
        MasterVerificationParser.TryParse("config: {\"foo\":\"bar\"}").Should().BeNull();
    }
}
