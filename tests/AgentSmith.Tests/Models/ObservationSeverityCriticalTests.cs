using System.Text.Json;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Models;

// Regression: the chain-analyst skill prompt instructs the LLM to emit
// `severity: "Critical"` for the highest impact, but ObservationSeverity
// historically had no Critical variant (High/Medium/Low/Info only). The
// FilterRoundHandler swallowed those observations via JsonException — silent
// drops in api-security-scan output. This pack asserts the new Critical
// member binds across the three entry points where severity flows in.
public sealed class ObservationSeverityCriticalTests
{
    [Theory]
    [InlineData("\"Critical\"", ObservationSeverity.Critical)]
    [InlineData("\"critical\"", ObservationSeverity.Critical)]
    [InlineData("\"CRITICAL\"", ObservationSeverity.Critical)]
    [InlineData("\"High\"", ObservationSeverity.High)]
    [InlineData("\"info\"", ObservationSeverity.Info)]
    public void JsonStringEnumConverter_AcceptsCriticalAcrossCasings(string json, ObservationSeverity expected)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var parsed = JsonSerializer.Deserialize<ObservationSeverity>(json, options);
        parsed.Should().Be(expected);
    }

    [Fact]
    public void ScannerObservationFactory_CriticalString_MapsToCriticalEnum()
    {
        ScannerObservationFactory.ParseSeverity("critical").Should().Be(ObservationSeverity.Critical);
        ScannerObservationFactory.ParseSeverity("crit").Should().Be(ObservationSeverity.Critical);
        ScannerObservationFactory.ParseSeverity("CRITICAL").Should().Be(ObservationSeverity.Critical);
    }

    [Fact]
    public void ObservationSummary_From_CountsCriticalSeparately()
    {
        var obs = new[]
        {
            ObservationFactory.Make("CRITICAL", "a.cs", 1, "t", "", 9),
            ObservationFactory.Make("CRITICAL", "a.cs", 2, "t", "", 9),
            ObservationFactory.Make("HIGH",     "a.cs", 3, "t", "", 9),
            ObservationFactory.Make("MEDIUM",   "a.cs", 4, "t", "", 9),
        };

        var summary = ObservationSummary.From(obs);

        summary.Critical.Should().Be(2);
        summary.High.Should().Be(1);
        summary.Medium.Should().Be(1);
        summary.Total.Should().Be(4);
    }
}
