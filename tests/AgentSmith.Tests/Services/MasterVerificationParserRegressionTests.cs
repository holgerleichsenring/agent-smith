using AgentSmith.Application.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

/// <summary>p0273: the verdict parser reads the raw failing-test lists (baseline +
/// final) so the keystone can diff them. Absent keys parse to null (back-compat →
/// binary gate).</summary>
public sealed class MasterVerificationParserRegressionTests
{
    [Fact]
    public void ParsesFailingTestsAndBaseline()
    {
        const string text = """
            Here is my verdict:
            ```verdict
            { "status": "failed", "build_ran": true, "build_passed": true,
              "tests_ran": true, "tests_passed": false,
              "failing_tests": ["A.One", "B.Two"],
              "baseline_failing_tests": ["A.One"],
              "summary": "B.Two broke" }
            ```
            """;

        var v = MasterVerificationParser.TryParse(text);

        v.Should().NotBeNull();
        v!.FailingTests.Should().BeEquivalentTo("A.One", "B.Two");
        v.BaselineFailingTests.Should().BeEquivalentTo("A.One");
    }

    [Fact]
    public void AbsentLists_ParseToNull()
    {
        const string text = """
            ```verdict
            { "status": "green", "build_ran": true, "build_passed": true,
              "tests_ran": true, "tests_passed": true, "summary": "ok" }
            ```
            """;

        var v = MasterVerificationParser.TryParse(text);

        v.Should().NotBeNull();
        v!.FailingTests.Should().BeNull();
        v.BaselineFailingTests.Should().BeNull();
    }
}
