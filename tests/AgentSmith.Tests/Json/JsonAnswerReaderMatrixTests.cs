using FluentAssertions;

namespace AgentSmith.Tests.Json;

/// <summary>
/// 2026-10-26-cb25d: every model-answer reader on one framing matrix — bare, fenced, prose, cited-before
/// ([c2] / {x}), cited-after, two-values, truncated, empty, none. "was" is what the reader answered before the
/// readers shared <see cref="AgentSmith.Application.Services.Json.JsonAnswerReader"/>; the changed cells and why
/// they changed are in decisions/2026-10-26-cb25d.yaml.
/// </summary>
public sealed class JsonAnswerReaderMatrixTests
{
    [Theory]
    [InlineData("SpecAccountReader",
        "2,2,2,null,null,null,null,null,null", "2,2,2,2,2,2,null,null,null")]
    [InlineData("FindingRefutationReader",
        "2,2,2,2,2,4,1,null,null", "2,2,2,2,2,2,1,null,null")]
    [InlineData("SpecCutAnswerReader",
        "2,2,2,2,2,2,null,null,null", "2,2,2,2,2,2,null,null,null")]
    [InlineData("SpecAttackAnswerReader",
        "build,build,build,build,build,build,null,null,null", "build,build,build,build,build,build,null,null,null")]
    [InlineData("SeriesContractAnswerReader",
        "build,build,build,build,build,build,null,null,null", "build,build,build,build,build,build,null,null,null")]
    [InlineData("ClientUsageReportReader",
        "1,1,1,null,null,null,null,null,null", "1,1,1,1,1,1,null,null,null")]
    [InlineData("StagedAuthFileJsonReader",
        "1,1,1,0,0,0,0,0,0", "1,1,1,1,1,1,0,0,0")]
    [InlineData("ProviderErrorPayloadParser",
        "overloaded,overloaded,overloaded,null,null,null,null,null,null",
        "overloaded,overloaded,overloaded,overloaded,overloaded,overloaded,null,null,null")]
    [InlineData("Application.LlmIntentParser",
        "12,12,12,throw,throw,throw,throw,throw,throw", "12,12,12,12,12,12,throw,throw,throw")]
    [InlineData("Server.LlmIntentParser",
        "12,null,null,null,null,null,null,null,null", "12,12,12,12,12,12,null,null,null")]
    public void Reader_OnTheFramingMatrix(string reader, string was, string now)
    {
        _ = was;
        JsonAnswerReaderMatrix.Outcomes(reader).Should().Be(now, $"{reader} answered {was} before 2026-10-26-cb25d");
    }
}
