using AgentSmith.Application.Services.SkillRounds;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.SkillRounds;

/// <summary>
/// p0167b: the blocking-downgrade confidence threshold is per-pipeline
/// configurable (ResolvedPipelineConfig.ConfidenceThreshold, default 70).
/// </summary>
public sealed class SkillResponseParserThresholdTests
{
    private readonly SkillResponseParser _sut = new(TolerantJsonParserFactory.CreateObservation());

    private const string BlockingAt65 = """
        [{"concern": "correctness", "description": "d", "severity": "high",
          "confidence": 65, "blocking": true}]
        """;

    [Fact]
    public void ParseAndDowngrade_BlockingBelowDefaultThreshold_DowngradedToNonBlocking()
    {
        var result = _sut.ParseAndDowngrade(BlockingAt65, "tester", NullLogger.Instance);

        result.Should().ContainSingle().Which.Blocking
            .Should().BeFalse("confidence 65 is below the default threshold of 70");
    }

    [Fact]
    public void ParseAndDowngrade_LoweredPipelineThreshold_KeepsBlocking()
    {
        var result = _sut.ParseAndDowngrade(
            BlockingAt65, "tester", NullLogger.Instance, confidenceThreshold: 60);

        result.Should().ContainSingle().Which.Blocking
            .Should().BeTrue("the pipeline lowered its threshold below the observation's confidence");
    }

    [Fact]
    public void ParseAndDowngrade_RaisedPipelineThreshold_DowngradesHigherConfidence()
    {
        const string blockingAt80 = """
            [{"concern": "correctness", "description": "d", "severity": "high",
              "confidence": 80, "blocking": true}]
            """;

        var result = _sut.ParseAndDowngrade(
            blockingAt80, "tester", NullLogger.Instance, confidenceThreshold: 90);

        result.Should().ContainSingle().Which.Blocking.Should().BeFalse();
    }
}
