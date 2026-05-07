using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

public sealed class ObservationParserTruncationTests
{
    [Fact]
    public void TruncatesDescription_WhenOverCap_AppendsMarkerAndPreservesObservation()
    {
        var longDescription = new string('x', 800);
        var response = $$"""[{"concern":"security","description":"{{longDescription}}","severity":"high","confidence":80,"blocking":false}]""";

        var result = ObservationParser.ParseWithoutIds(response, "test-skill", NullLogger.Instance);

        result.Should().HaveCount(1);
        result[0].Description.Length.Should().BeLessThanOrEqualTo(ObservationCaps.DescriptionMaxChars);
        result[0].Description.Should().Contain("[truncated, original was 800 chars]");
    }

    [Fact]
    public void TruncatesSuggestion_WhenOverCap()
    {
        var longSuggestion = new string('y', 500);
        var response = $$"""[{"concern":"security","description":"short","suggestion":"{{longSuggestion}}","severity":"high","confidence":80,"blocking":false}]""";

        var result = ObservationParser.ParseWithoutIds(response, "test-skill", NullLogger.Instance);

        result.Should().HaveCount(1);
        result[0].Suggestion.Length.Should().BeLessThanOrEqualTo(ObservationCaps.SuggestionMaxChars);
        result[0].Suggestion.Should().Contain("[truncated");
    }

    [Fact]
    public void TruncatesRationale_WhenOverCap()
    {
        var longRationale = new string('z', 800);
        var response = $$"""[{"concern":"security","description":"short","rationale":"{{longRationale}}","severity":"high","confidence":80,"blocking":false}]""";

        var result = ObservationParser.ParseWithoutIds(response, "test-skill", NullLogger.Instance);

        result.Should().HaveCount(1);
        result[0].Rationale!.Length.Should().BeLessThanOrEqualTo(ObservationCaps.RationaleMaxChars);
        result[0].Rationale.Should().Contain("[truncated");
    }

    [Fact]
    public void TruncatesDetails_WhenOverCap()
    {
        var longDetails = new string('d', 5000);
        var response = $$"""[{"concern":"security","description":"short","details":"{{longDetails}}","severity":"high","confidence":80,"blocking":false}]""";

        var result = ObservationParser.ParseWithoutIds(response, "test-skill", NullLogger.Instance);

        result.Should().HaveCount(1);
        result[0].Details!.Length.Should().BeLessThanOrEqualTo(ObservationCaps.DetailsMaxChars);
        result[0].Details.Should().Contain("[truncated");
    }

    [Fact]
    public void UnderCap_NoTruncationApplied()
    {
        const string normalDescription = "OAuth2 implicit flow lacks refresh-token support; long-lived tokens issued.";
        var response = $$"""[{"concern":"security","description":"{{normalDescription}}","severity":"high","confidence":80,"blocking":false}]""";

        var result = ObservationParser.ParseWithoutIds(response, "test-skill", NullLogger.Instance);

        result.Should().HaveCount(1);
        result[0].Description.Should().Be(normalDescription);
        result[0].Description.Should().NotContain("[truncated");
    }

    [Fact]
    public void TruncationWarning_LoggedOncePer_RoleField()
    {
        // Two over-cap observations in the same skill+field — should warn once, not twice.
        var longA = new string('a', 800);
        var longB = new string('b', 700);
        var response = $$"""[{"concern":"security","description":"{{longA}}","severity":"high","confidence":80,"blocking":false},{"concern":"security","description":"{{longB}}","severity":"medium","confidence":70,"blocking":false}]""";

        var sink = new ListLoggerSink();
        var logger = sink.CreateLogger();

        var result = ObservationParser.ParseWithoutIds(response, "test-skill", logger);

        result.Should().HaveCount(2);
        sink.Warnings.Count(w => w.Contains("description") && w.Contains("truncated"))
            .Should().Be(1, "warning should be deduped per (role, field)");
    }
}

internal sealed class ListLoggerSink
{
    public List<string> Warnings { get; } = new();

    public Microsoft.Extensions.Logging.ILogger CreateLogger() => new SinkLogger(Warnings);

    private sealed class SinkLogger(List<string> warnings) : Microsoft.Extensions.Logging.ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
            TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == Microsoft.Extensions.Logging.LogLevel.Warning)
                warnings.Add(formatter(state, exception));
        }
    }
}
