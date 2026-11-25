using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Series;
using FluentAssertions;

namespace AgentSmith.Tests.Series;

/// <summary>
/// 2026-11-25-9992i: the final pull request's body is what the person reviewing an end series reads — a section per ticket with
/// its title and pull requests, the master's acts on it with their reasons, and what it could not verify.
/// </summary>
public sealed class SeriesFinalBodyTests
{
    private const string Branch = "agent-smith/series/sr-1";

    [Fact]
    public void Of_HasASectionPerTicket_WithItsPullRequestsActsAndReasons()
    {
        var record = SeriesRecords.Running() with
        {
            Acts =
            [
                new SeriesAct(SeriesRecords.T, "merge_pull_request", "43", "merged SCRUM-43 (now)", "build and tests green"),
                new SeriesAct(SeriesRecords.T, "refresh_branch", "44", "refreshed SCRUM-44", null),
                new SeriesAct(SeriesRecords.T, "series_status", null, "read the series", "a look"),
            ],
        };

        var body = SeriesFinalBody.Of(record, Branch, Facts());

        body.Should().StartWith($"Delivers series sr-1 from `{Branch}` into the default branch");
        body.Should().Contain("## SCRUM-43: work").And.Contain("## SCRUM-44: work").And.Contain("## SCRUM-45: work");
        body.Should().Contain("- Pull requests: api [#1](https://host/pr/43/1)");
        body.Should().Contain("  - merged SCRUM-43 (now) — build and tests green").And.Contain("  - refreshed SCRUM-44\n");
        body.Should().Contain("- The series master did not act on it").And.NotContain("read the series");
    }

    [Fact]
    public void Of_SaysWhatItCouldNotVerify_ARedOrUnreadCiOnTheLastHead()
    {
        var body = SeriesFinalBody.Of(SeriesRecords.Running(), Branch, Facts());

        Section(body, "SCRUM-43").Should().Contain("- Verified: CI green on every pull request's last head");
        Section(body, "SCRUM-44").Should().Contain("- Not verified: CI on the last head of api (red)");
        Section(body, "SCRUM-45").Should().Contain("- Pull requests: none read").And.Contain("- Verified");
    }

    [Fact]
    public void Of_AGitHubIssue_KeepsItsClosingKeyword()
    {
        var record = SeriesRecords.Running() with { Tickets = [new SeriesTicket("7", "#7", "fix it", false, SeriesRecords.T)] };

        SeriesFinalBody.Of(record, Branch, []).Should().Contain("## #7: fix it\n\nCloses #7\n");
    }

    [Fact]
    public void Of_ACiNeverRead_IsNotVerified()
    {
        var facts = new[] { SeriesRecords.Fact("43", landed: SeriesRecords.T).WithPr(PrReviewStates.Merged, null) };

        SeriesFinalBody.Of(SeriesRecords.Running(), Branch, facts).Should().Contain("Not verified: CI on the last head of api (not read)");
    }

    private static SeriesTicketFact[] Facts() =>
    [
        SeriesRecords.Fact("43", landed: SeriesRecords.T).WithPr(PrReviewStates.Merged, CiStates.Green),
        SeriesRecords.Fact("44", landed: SeriesRecords.T).WithPr(PrReviewStates.Merged, CiStates.Red),
        SeriesRecords.Fact("45", landed: SeriesRecords.T),
    ];

    private static string Section(string body, string key)
    {
        var start = body.IndexOf($"## {key}:", StringComparison.Ordinal);
        var next = body.IndexOf("\n## ", start + 1, StringComparison.Ordinal);
        return next < 0 ? body[start..] : body[start..next];
    }
}
