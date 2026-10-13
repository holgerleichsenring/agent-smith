using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.Tickets;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-10-13-9a59o: ticket_runs carries each attempt's cause, each spec's judgement and the
/// ticket's pull request reviews — read over a real migrated store through the real readers.
/// </summary>
public sealed class TicketRunsFactsTests : IDisposable
{
    private const string Pr = "https://github.com/o/r/pull/7";
    private readonly TicketStoreHarness _store = new();

    [Fact]
    public async Task TicketRuns_CarriesCauseOfEachAttempt()
    {
        await _store.AddAsync(TicketStoreHarness.Run("r-1"), TicketStoreHarness.Run("r-2"),
            new RunAttempt { RunId = "r-1", CauseChannel = "pickup", CauseAt = TicketStoreHarness.T },
            new RunAttempt
            {
                RunId = "r-2", CauseChannel = "operator", CauseActor = "dana", CauseAt = TicketStoreHarness.T.AddHours(1),
                SourceKind = "conversation",
            });

        var runs = (await AnswerAsync()).Runs;

        runs.Single(r => r.RunId == "r-1").Cause.Should().Be(new TicketRunCause("pickup", null, TicketStoreHarness.T, null));
        runs.Single(r => r.RunId == "r-2").Cause.Should().Be(
            new TicketRunCause("operator", "dana", TicketStoreHarness.T.AddHours(1), "conversation"));
        runs.Should().OnlyContain(r => r.Pipeline == "code");
    }

    [Fact]
    public async Task TicketRuns_RunWithoutAttemptRow_HasNullCause()
    {
        await _store.AddAsync(TicketStoreHarness.Run("r-1"));

        (await AnswerAsync()).Runs.Single().Cause.Should().BeNull();
    }

    [Fact]
    public async Task TicketRuns_CarriesSpecVerdictAndCriteriaOutcomes()
    {
        var spec = Judged("r-1", "s-1", "failed", new AcceptanceView(
            [new AcceptanceCriterionView("Export writes CSV", "met", "export test green", "src/Export.cs:12"),
             new AcceptanceCriterionView("Header row present", "unmet", "no header written", null)],
            "verbatim", "dana"));
        spec.Verdict = "header missing";
        await _store.AddAsync(TicketStoreHarness.Run("r-1"), spec);

        var answered = (await AnswerAsync()).Runs.Single().Specs.Single();

        answered.Should().BeEquivalentTo(new TicketRunSpec("s-1", "failed", "header missing",
            [new TicketRunCriterion("Export writes CSV", "met", "export test green", "src/Export.cs:12"),
             new TicketRunCriterion("Header row present", "unmet", "no header written", null)], [], []));
    }

    [Fact]
    public async Task TicketRuns_DeclinedAndUnmeasured_Carried()
    {
        await _store.AddAsync(TicketStoreHarness.Run("r-1"), Judged("r-1", "s-1", "failed", new AcceptanceView(
            [], "verbatim", "dana", null,
            [new DeclinedCriterionView("Migrate the legacy store", "out of this repository", "s-1")],
            [new UnmeasuredAccountView("api", "build red")])));

        var spec = (await AnswerAsync()).Runs.Single().Specs.Single();

        spec.Declined.Should().ContainSingle().Which.Should().Be(new TicketRunDeclined("Migrate the legacy store", "out of this repository"));
        spec.Unmeasured.Should().ContainSingle().Which.Should().Be(new TicketRunUnmeasured("api", "build red"));
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{}")]
    public async Task TicketRuns_UndecodableAcceptanceJson_ReadsNullCriteria(string json)
    {
        var spec = TicketStoreHarness.Spec("r-1", "s-1", "done");
        spec.AcceptanceJson = json;
        await _store.AddAsync(TicketStoreHarness.Run("r-1"), spec);

        var answered = (await AnswerAsync()).Runs.Single().Specs.Single();

        answered.Id.Should().Be("s-1");
        answered.Criteria.Should().BeNull();
        answered.Declined.Should().BeNull();
        answered.Unmeasured.Should().BeNull();
    }

    [Fact]
    public async Task TicketRuns_CarriesPrReviewCommentWithFileAndLine()
    {
        await _store.AddAsync(TicketStoreHarness.Run("r-1"), Record(Pr, "r-1"),
            Entry(Pr, "c-1", "comment", 1, author: "rev", file: "src/Export.cs", line: 40, body: "Header is missing"));

        var entry = (await AnswerAsync()).PullRequestReviews.Single().Entries.Single();

        entry.Should().BeEquivalentTo(new
        {
            Kind = "comment", Author = "rev", File = "src/Export.cs", Line = 40, Body = "Header is missing",
            Trusted = true, Truncated = false,
        });
    }

    [Fact]
    public async Task TicketRuns_CarriesReviewDecisionAndKind()
    {
        await _store.AddAsync(TicketStoreHarness.Run("r-1"), Record(Pr, "r-1"),
            Entry(Pr, "d-1", "decision", 1, author: "rev", decision: "changes_requested", standing: true),
            Entry(Pr, "d-2", "review", 2, author: "lead", decision: "approved"));

        var entries = (await AnswerAsync()).PullRequestReviews.Single().Entries;

        entries.Select(e => (e.Kind, e.Decision, e.Author, e.Standing)).Should().Equal(
            ("decision", "changes_requested", "rev", true), ("review", "approved", "lead", false));
    }

    [Fact]
    public async Task TicketRuns_SystemAndBotEntries_Flagged()
    {
        await _store.AddAsync(TicketStoreHarness.Run("r-1"), Record(Pr, "r-1"),
            Entry(Pr, "s-1", "comment", 1, system: true, trusted: false),
            Entry(Pr, "b-1", "comment", 2, bot: true, trusted: false),
            Entry(Pr, "o-1", "comment", 3, ours: true));

        var entries = (await AnswerAsync()).PullRequestReviews.Single().Entries;

        entries.Select(e => (e.System, e.Bot, e.Ours, e.Trusted)).Should().Equal(
            (true, false, false, false), (false, true, false, false), (false, false, true, true));
    }

    [Fact]
    public async Task TicketRuns_PrNamedByReadableUrl_NotPrKey()
    {
        await _store.AddAsync(TicketStoreHarness.Run("r-1"), Record(Pr, "r-1", state: "merged"));

        var review = (await AnswerAsync()).PullRequestReviews.Single();

        review.Url.Should().Be(Pr);
        review.State.Should().Be("merged");
        review.Url.Should().NotContain(PrKey.Of(Pr));
    }

    [Fact]
    public async Task TicketRuns_PrReviewEntries_CappedNewestFortyOverAllPullRequests()
    {
        const string other = "https://github.com/o/r/pull/8";
        var rows = new List<object> { TicketStoreHarness.Run("r-1"), Record(Pr, "r-1"), Record(other, "r-1") };
        for (var i = 0; i < 30; i++) rows.Add(Entry(Pr, $"a-{i}", "comment", i * 2));
        for (var i = 0; i < 30; i++) rows.Add(Entry(other, $"b-{i}", "comment", i * 2 + 1));
        await _store.AddAsync([.. rows]);

        var reviews = (await AnswerAsync()).PullRequestReviews;

        reviews.SelectMany(r => r.Entries).Should().HaveCount(TicketRunsReviews.EntryCap);
        reviews.SelectMany(r => r.Entries).Min(e => e.At).Should().Be(TicketStoreHarness.T.AddMinutes(20));
        reviews.Should().AllSatisfy(r => r.Entries.Select(e => e.At).Should().BeInAscendingOrder());
        reviews.Select(r => r.Entries.Count).Should().Equal(20, 20);
    }

    [Fact]
    public async Task TicketRuns_LongBodies_TruncatedInAnswer()
    {
        await _store.AddAsync(TicketStoreHarness.Run("r-1"), Record(Pr, "r-1"),
            Entry(Pr, "long", "comment", 1, body: new string('x', 1000)),
            Entry(Pr, "stored-cut", "comment", 2, body: "short", truncated: true));

        var entries = (await AnswerAsync()).PullRequestReviews.Single().Entries;

        entries[0].Body.Should().HaveLength(TicketRunsReviews.BodyCap);
        entries[0].Truncated.Should().BeTrue();
        entries[1].Body.Should().Be("short");
        entries[1].Truncated.Should().BeTrue();
    }

    [Fact]
    public async Task TicketRuns_PrOfOlderAttempt_ReadByTicket()
    {
        var rows = new List<object>();
        for (var i = 1; i <= 11; i++) rows.Add(TicketStoreHarness.Run($"r-{i:00}"));
        rows.Add(Record(Pr, "r-01"));
        rows.Add(Entry(Pr, "c-1", "comment", 1, body: "seen on the first attempt"));
        await _store.AddAsync([.. rows]);

        var answer = await AnswerAsync();

        answer.Runs.Should().NotContain(r => r.RunId == "r-01");
        answer.PullRequestReviews.Single().Entries.Single().Body.Should().Be("seen on the first attempt");
    }

    [Fact]
    public async Task TicketRuns_StoreFails_ReturnsNone()
    {
        await _store.AddAsync(TicketStoreHarness.Run("r-1"));
        var broken = new Mock<IServiceScopeFactory>();
        broken.Setup(f => f.CreateScope()).Throws(new InvalidOperationException("store down"));

        var answer = await Bound(new TicketRunsFacts(broken.Object)).ForAsync(CancellationToken.None);

        answer.Should().BeSameAs(TicketRunsResult.None);
    }

    public void Dispose() => _store.Dispose();

    private Task<TicketRunsResult> AnswerAsync() =>
        Bound(ActivatorUtilities.CreateInstance<TicketRunsFacts>(_store.Services)).ForAsync(CancellationToken.None);

    private BoundTicketRuns Bound(TicketRunsFacts facts) =>
        new(_store.Services.GetRequiredService<FiledWorkRunsReader>(), facts, _store.Config.Projects["alpha"], "42",
            NullLogger.Instance);

    private static RunSpec Judged(string runId, string specId, string status, AcceptanceView view)
    {
        var spec = TicketStoreHarness.Spec(runId, specId, status);
        spec.AcceptanceJson = RunStoryJson.Serialize(view);
        return spec;
    }

    private static PrReviewRecord Record(string url, string runId, string state = "open") => new()
    {
        PrKey = PrKey.Of(url), Url = url, Repo = "o/r", RepoKey = "o/r", Number = url[(url.LastIndexOf('/') + 1)..],
        Project = "alpha", TicketId = "42", RunId = runId, State = state,
        StateAtTicks = TicketStoreHarness.T.UtcTicks, CheckedAtTicks = TicketStoreHarness.T.UtcTicks,
    };

    private static PrReviewEntry Entry(
        string url, string hostId, string kind, int minute, string? author = "rev", string? decision = null,
        bool standing = false, bool trusted = true, bool ours = false, bool bot = false, bool system = false,
        string? file = null, int? line = null, string body = "body", bool truncated = false) => new()
    {
        PrKey = PrKey.Of(url), HostId = hostId, Kind = kind, Decision = decision, Standing = standing,
        AuthorLogin = author, Trusted = trusted, IsOurs = ours, IsBot = bot, IsSystem = system, File = file, Line = line,
        Body = body, Truncated = truncated, AtTicks = TicketStoreHarness.T.AddMinutes(minute).UtcTicks,
    };
}
