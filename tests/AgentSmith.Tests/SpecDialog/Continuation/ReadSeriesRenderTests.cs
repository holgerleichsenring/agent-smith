using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Dialogue;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog.Continuation;

/// <summary>2026-11-18-d6d2b: a ticket whose record names a series the branch did not answer reads as a failed read, not
/// as an empty series the steward could conclude there is nothing to run from.</summary>
public sealed class ReadSeriesRenderTests
{
    [Fact]
    public void Render_RecordNamesASeries_BranchAnsweredNone_SaysTheReadFailed() =>
        ReadSeriesToolHost.Render(new SeriesReading(null, "2026-11-18-0a1b", [], new Dictionary<string, string>()))
            .Should().Contain("names series 2026-11-18-0a1b").And.Contain("failed read, not an empty series")
            .And.Contain("do not conclude there is nothing to run");

    [Fact]
    public void Render_NoSeriesRecorded_NoSpec_SaysTheSeriesHoldsNone() =>
        ReadSeriesToolHost.Render(new SeriesReading(null, null, [], new Dictionary<string, string>()))
            .Should().Be("The ticket's series holds no spec.");
}
