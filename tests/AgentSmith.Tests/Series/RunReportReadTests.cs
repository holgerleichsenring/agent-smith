using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-26-a12ed: the series master reads a ticket's newest run report — the coding masters' own markdown, kept on the
/// run row's acceptance snapshot — through read_ticket, and a wake names its headings.</summary>
public sealed class RunReportReadTests : IDisposable
{
    private readonly ServerStateStore _store = new();

    public void Dispose() => _store.Dispose();

    [Fact]
    public void Of_OneReportAsWritten_SeveralUnderTheirSpecIds()
    {
        RunReportText.Of(View(new MasterReportView("## Delivered\nx"))).Should().Be("## Delivered\nx");
        RunReportText.Of(View(new MasterReportView("a", "s-1"), new MasterReportView("b", "s-2")))
            .Should().Be("## Spec s-1\n\na\n\n## Spec s-2\n\nb");
        RunReportText.Of(View()).Should().BeNull();
    }

    [Fact]
    public void Headings_AreTheSections_OrTheFirstLine()
    {
        RunReportText.Headings("# Report\n\n## Follow-ups\n- y").Should().Be("Report · Follow-ups");
        RunReportText.Headings("\nDelivered the endpoint.\nMore.").Should().Be("Delivered the endpoint.");
        RunReportText.Headings("  ").Should().BeNull();
    }

    [Fact]
    public async Task DbRunReportReader_ReadsTheRunRowsSnapshot()
    {
        await using (var db = _store.Context())
        {
            db.Add(new Run { Id = "r-1", Project = "p", Pipeline = "code", TicketId = "1", Status = RunStatuses.Success,
                StartedAt = DateTimeOffset.UnixEpoch, AcceptanceJson = RunStoryJson.Serialize(View(new MasterReportView("## Open\nthe retry"))) });
            await db.SaveChangesAsync();
        }

        var reader = new DbRunReportReader(_store.ScopeFactory);

        (await reader.ReportAsync("r-1", default)).Should().Be("## Open\nthe retry");
        (await reader.ReportAsync("r-2", default)).Should().BeNull();
    }

    [Fact]
    public async Task ReadTicket_CarriesTheNewestRunsReport()
    {
        var fact = SeriesRecords.Fact("43", RunStatuses.Success) with { LatestRunId = "run-9" };
        var reports = Mock.Of<IRunReportReader>(r => r.ReportAsync("run-9", It.IsAny<CancellationToken>())
            == Task.FromResult<string?>("## Deviations\nkept the old route"));

        var json = await new SeriesReadTools(new SeriesBeat(SeriesRecords.Running(), [fact], SeriesRecords.T), reports).ReadTicketAsync("SCRUM-43");

        json.Should().Contain("\"report\":\"## Deviations\\nkept the old route\"");
    }

    private static AcceptanceView View(params MasterReportView[] reports) => new([], "verbatim", "op", Reports: reports);
}
