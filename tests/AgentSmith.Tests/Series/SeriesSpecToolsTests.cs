using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Series;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-27-3532d: retire_spec reaches the retirement with the ticket's id and says the act with the master's
/// reason; a refused retirement says nothing, and an approver without runs.control retires nothing.</summary>
public sealed class SeriesSpecToolsTests
{
    private readonly Mock<ISeriesSpecRetirement> _retirement = new();
    private SeriesBeat _beat = null!;

    private SeriesSpecTools Tools(bool mayStartRuns = true)
    {
        var record = SeriesRecords.Running(mayStartRuns: mayStartRuns);
        _beat = new SeriesBeat(record, [.. record.Tickets.Select(t => new SeriesTicketFact(t))], SeriesRecords.T);
        return new SeriesSpecTools(_beat, _retirement.Object);
    }

    [Fact]
    public async Task RetireSpec_ReachesTheRetirementWithTheTicketsId_AndSaysIt()
    {
        var tools = Tools();
        _retirement.Setup(r => r.RetireAsync(It.IsAny<SeriesRunRecord>(), "44", "s-b", "yaml", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SeriesCutAnswer("Recorded for the next attempt.", true, "44"));

        var said = await tools.RetireSpecAsync("SCRUM-44", "s-b", "yaml", "the store it needs does not exist");

        said.Should().Be("Recorded for the next attempt.");
        (_beat.ActTicket, _beat.ActDid).Should().Be(("44", "retired s-b of SCRUM-44"));
        _beat.Acts.Should().ContainSingle().Which.Notice.Should().EndWith("retired s-b of SCRUM-44: the store it needs does not exist");
    }

    [Fact]
    public async Task RetireSpec_Refused_SaysNothing()
    {
        var tools = Tools();
        _retirement.Setup(r => r.RetireAsync(It.IsAny<SeriesRunRecord>(), "44", "s-b", "yaml", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SeriesCutAnswer("Nothing was recorded: it ran.", false));

        (await tools.RetireSpecAsync("SCRUM-44", "s-b", "yaml", "why")).Should().Be("Nothing was recorded: it ran.");
        _beat.Acts.Should().BeEmpty();
    }

    [Fact]
    public async Task RetireSpec_WithoutRunsControl_RetiresNothing()
    {
        var tools = Tools(mayStartRuns: false);

        (await tools.RetireSpecAsync("SCRUM-44", "s-b", "yaml")).Should().Contain("runs.control");
        _retirement.VerifyNoOtherCalls();
    }
}
