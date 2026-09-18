using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-17-042em: a filing report carried web urls, so what was just filed read as a column of
/// links differing in their last few digits. The KEY is the name a person repeats and types into a
/// tracker's search, and it is formatted from the id 2026-09-17-042eg put on the report.
/// </summary>
public sealed class FiledTicketKeyTests
{
    [Theory]
    [InlineData(TrackerType.Jira, "SAMPLE-412", "SAMPLE-412")]
    [InlineData(TrackerType.GitHub, "412", "#412")]
    [InlineData(TrackerType.GitLab, "412", "#412")]
    [InlineData(TrackerType.AzureDevOps, "412", "#412")]
    public void FiledTicket_EachTracker_CarriesItsDisplayKey(
        TrackerType tracker, string id, string expected)
    {
        var created = new CreatedTicket(new TicketId(id), "https://tracker.test/x");

        var entry = OutcomeTicketFiler.Entry(created, "p9001: the phase", Project(tracker));

        entry.Key.Should().Be(expected);
        entry.TicketId.Should().Be(id, "the key is formatted FROM the id, which stays on the report");
    }

    [Fact]
    public void FilingNotice_OnTheDashboard_LinksTheKeyToTheTicket()
    {
        var notice = Notice(Filed("SAMPLE-412", "https://tracker.test/browse/SAMPLE-412"))
            .In(SpecDialogMarkup.CommonMark);

        notice.Should().Contain("[SAMPLE-412](https://tracker.test/browse/SAMPLE-412) — p9001: the phase");
    }

    /// <summary>
    /// A CHAT reader gets the key too. The phase claims filed tickets read as keys rather than raw
    /// urls, and a claim that held on one channel only would be the wrong half of it; the link
    /// shape is the channel's, which is what SpecDialogMarkup.Link is for.
    /// </summary>
    [Fact]
    public void FilingNotice_InChat_LinksTheKeyToTheTicket()
    {
        var notice = Notice(Filed("SAMPLE-412", "https://tracker.test/browse/SAMPLE-412"))
            .In(SpecDialogMarkup.ChatMrkdwn);

        notice.Should().Contain("<https://tracker.test/browse/SAMPLE-412|SAMPLE-412> — p9001: the phase")
            .And.NotContain("[SAMPLE-412]", "a CommonMark link shape is not chat mrkdwn's");
    }

    [Fact]
    public void Markup_Link_IsTheChannelsOwnShape()
    {
        SpecDialogMarkup.CommonMark.Link("SAMPLE-412", "https://tracker.test/x")
            .Should().Be("[SAMPLE-412](https://tracker.test/x)");
        SpecDialogMarkup.ChatMrkdwn.Link("SAMPLE-412", "https://tracker.test/x")
            .Should().Be("<https://tracker.test/x|SAMPLE-412>");
    }

    /// <summary>A tracker that gives no web url: the key is the name, with nothing to link it to.
    /// On either channel — a link to "#412" navigates nowhere and claims it does.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FilingNotice_WithoutAWebUrl_IsTheBareKey(bool onThePage)
    {
        var notice = Notice(Filed("#412", "#412"))
            .In(onThePage ? SpecDialogMarkup.CommonMark : SpecDialogMarkup.ChatMrkdwn);

        notice.Should().Contain("- #412 — p9001: the phase")
            .And.NotContain("](").And.NotContain("|#412>");
    }

    /// <summary>A prefix test would admit "httpfoo://x"; the panel's own rule is ^https?://.</summary>
    [Fact]
    public void FilingNotice_AReferenceThatOnlyStartsWithHttp_IsNotLinked()
    {
        var notice = Notice(Filed("#412", "httpfoo://tracker.test/7")).In(SpecDialogMarkup.CommonMark);

        notice.Should().Contain("- #412 — p9001: the phase").And.NotContain("](");
    }

    /// <summary>
    /// A filing stored before this phase has no key in its row. It must read back as ABSENT and
    /// fall back to the reference it always showed — a key invented from an id the row does not
    /// carry either would be a name that resolves to nothing.
    /// </summary>
    [Fact]
    public void LatestFiling_RowWithoutKey_ReadsAndFallsBackToTheReference()
    {
        const string stored =
            """{"filed":[{"reference":"https://tracker.test/7","title":"p9001: the phase"}],"error":null,"at":"2026-09-17T10:00:00Z","kind":"phase","notes":[]}""";

        var filing = JsonSerializer.Deserialize<SpecDialogFiling>(
            stored, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var ticket = filing!.Filed.Should().ContainSingle().Subject;
        ticket.Key.Should().BeNull();
        Notice(ticket).In(SpecDialogMarkup.CommonMark).Should()
            .Contain("https://tracker.test/7 — p9001: the phase").And.NotContain("](");
    }

    private static ComposedReply Notice(FiledTicket ticket) =>
        new SpecDialogOutcomeComposer().ComposeFiled(
            new PhaseOutcome(new PhaseDraft("p9001", "the phase", "phase: p9001", [])),
            new FilingReport([ticket], Error: null));

    private static FiledTicket Filed(string key, string reference) =>
        new(reference, "p9001: the phase") { Key = key, TicketId = key };

    private static ResolvedProject Project(TrackerType tracker) => new()
    {
        Name = "proj",
        DefaultPipeline = "code",
        Tracker = new TrackerConnection { Name = "sample-tracker", Type = tracker },
    };
}
