using AgentSmith.Application.Services.Builders;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Builders;

public sealed class TrxResultParserTests
{
    private readonly TrxResultParser _parser = new();

    [Fact]
    public void Parse_ValidTrx_ExtractsCounters()
    {
        var trx = BuildTrx(passed: 9, failed: 2, total: 11);

        var summary = _parser.Parse(trx);

        summary.TotalCount.Should().Be(11);
        summary.PassedCount.Should().Be(9);
        summary.FailedCount.Should().Be(2);
    }

    [Fact]
    public void Parse_AllPassed_ReturnsNoFailures()
    {
        var trx = BuildTrx(passed: 5, failed: 0, total: 5, includeFailures: false);

        var summary = _parser.Parse(trx);

        summary.Failures.Should().BeEmpty();
    }

    [Fact]
    public void Parse_WithFailures_ExtractsTestNamesAndErrorMessages()
    {
        var trx = BuildTrx(passed: 0, failed: 2, total: 2);

        var summary = _parser.Parse(trx);

        summary.Failures.Should().HaveCount(2);
        summary.Failures[0].TestName.Should().Be("MyClass.TestA");
        summary.Failures[0].ErrorMessage.Should().Contain("expected 1 but was 2");
        summary.Failures[0].StackTrace.Should().Contain("at MyClass.TestA");
        summary.Failures[1].TestName.Should().Be("MyClass.TestB");
    }

    [Fact]
    public void Parse_EmptyOrInvalidXml_ReturnsEmpty()
    {
        _parser.Parse("").Should().BeSameAs(_parser.Parse("") /* idempotent */);
        _parser.Parse("<not-a-trx>").TotalCount.Should().Be(0);
        _parser.Parse("garbage").TotalCount.Should().Be(0);
    }

    [Fact]
    public void Parse_MissingCounters_ReturnsZeroes()
    {
        const string trx = "<TestRun xmlns=\"http://microsoft.com/schemas/VisualStudio/TeamTest/2010\"></TestRun>";

        var summary = _parser.Parse(trx);

        summary.TotalCount.Should().Be(0);
        summary.PassedCount.Should().Be(0);
        summary.FailedCount.Should().Be(0);
        summary.Failures.Should().BeEmpty();
    }

    private static string BuildTrx(int passed, int failed, int total, bool includeFailures = true)
    {
        var failureNodes = includeFailures && failed > 0
            ? string.Join("", Enumerable.Range(0, failed).Select(i =>
                $"""
                <UnitTestResult outcome="Failed" testName="MyClass.Test{(char)('A' + i)}">
                  <Output>
                    <ErrorInfo>
                      <Message>expected 1 but was 2</Message>
                      <StackTrace>at MyClass.Test{(char)('A' + i)}() in /src/MyClass.cs:line 10</StackTrace>
                    </ErrorInfo>
                  </Output>
                </UnitTestResult>
                """))
            : string.Empty;

        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
              <ResultSummary outcome="{(failed == 0 ? "Completed" : "Failed")}">
                <Counters total="{total}" passed="{passed}" failed="{failed}" />
              </ResultSummary>
              <Results>
                {failureNodes}
              </Results>
            </TestRun>
            """;
    }
}
