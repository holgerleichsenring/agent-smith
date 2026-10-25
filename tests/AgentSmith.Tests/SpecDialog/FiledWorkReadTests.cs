using System.Text.Json;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Application.Services.SpecExecution;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using Contracts = AgentSmith.Contracts;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-17-042ej: what the conversation that filed work is served — ticket, then run, then
/// SPEC — over a REAL SQLite store, the real session row and the real latest-filing writer.
/// </summary>
[Collection(RelationalStoreCollection.Name)]
public sealed class FiledWorkReadTests : IDisposable
{
    private const string Platform = DispatcherDefaults.PlatformDashboard;
    private const string Dialog = "d-042ej";
    private const string Owner = "person-a";
    private const string Work = "1001";
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-09-17T09:00:00Z");

    private readonly SqliteConnection _connection;
    private readonly IServiceScopeFactory _scopes;

    public FiledWorkReadTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using (var ctx = new AgentSmithDbContext(Options())) MigratedStoreTemplate.CopyInto(ctx);
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        services.AddScoped<RunCheckpointRepository>();
        _scopes = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task FiledWork_WorkTicketWithTwoRuns_ListsBothNewestFirst()
    {
        await FilingAsync(Filed(Work, "alpha"));
        await RunsAsync(
            Run("2026-09-17T09-00-00-0001", "alpha", Work, T),
            Run("2026-09-17T11-00-00-0002", "alpha", Work, T.AddHours(2)));

        var ticket = (await ReadAsync()).Tickets.Single();

        ticket.Runs.Select(r => r.RunId).Should()
            .Equal("2026-09-17T11-00-00-0002", "2026-09-17T09-00-00-0001");
    }

    // 2026-10-13-9a59a: ForAsync now composes through ForRunsAsync; its callers keep the cap and every pipeline.
    [Fact]
    public async Task FiledWorkRunsReader_ExistingCallers_KeepTheCapAndEveryPipeline()
    {
        await FilingAsync(Filed(Work, "alpha"));
        var runs = Enumerable.Range(1, 12).Select(i => Run($"2026-09-17T09-00-{i:00}-000{i % 10}", "alpha", Work, T)).ToArray();
        runs[^1].Pipeline = "pr-review";
        await RunsAsync(runs);

        var read = (await ReadAsync()).Tickets.Single().Runs;

        read.Should().HaveCount(10);
        read[0].RunId.Should().Be(runs[^1].Id);
        read[0].Pipeline.Should().Be("pr-review");
    }

    [Fact]
    public async Task FiledWork_RunWithFiveSpecs_ListsEachSpecWithItsStanding()
    {
        await FilingAsync(Filed(Work, "alpha"));
        await RunsAsync(Run("r-1", "alpha", Work, T));
        await SpecsAsync(
            Spec("r-1", "p1", 1, "done"), Spec("r-1", "p2", 2, "done"),
            Spec("r-1", "p3", 3, "in_progress"), Spec("r-1", "p4", 4, "not_started"),
            Spec("r-1", "p5", 5, "not_started"));

        var specs = (await ReadAsync()).Tickets.Single().Runs.Single().Specs;

        specs.Select(p => p.SpecId).Should().Equal("p1", "p2", "p3", "p4", "p5");
        specs.Select(p => p.Status).Should()
            .Equal("done", "done", "in_progress", "not_started", "not_started");
    }

    /// <summary>2026-10-06-03c7g: the payload names a run's specs as specs/specId.</summary>
    [Fact]
    public async Task FiledWork_RunPayload_SerializesSpecsAndSpecId()
    {
        await FilingAsync(Filed(Work, "alpha"));
        await RunsAsync(Run("r-1", "alpha", Work, T));
        await SpecsAsync(Spec("r-1", "p1", 1, "done"));

        var run = (await ReadAsync()).Tickets.Single().Runs.Single();
        var json = JsonSerializer.Serialize(run, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        json.Should().Contain("\"specs\":[{\"specId\":\"p1\"").And.NotContain("\"phases\"");
    }

    /// <summary>
    /// 2026-09-22-b3d7: nothing writes this state any more, and every filing stored before this
    /// spec still carries it. The enum is serialized BY NAME and an unreadable filing row is
    /// shown as ABSENT, so a member that went missing would not surface as a missing word but as
    /// the whole stored filing silently vanishing from the pane.
    /// </summary>
    [Fact]
    public async Task FiledWork_AStoredFilingCarryingARecordState_StillReadsBack()
    {
        await FilingAsync(
            Filed(Work, "alpha"),
            Filed("1002", "alpha") with { Start = new FiledWorkStart(FiledStartState.Record, "a record") });
        // A record is not work, so even a run that names its id is not shown against it.
        await RunsAsync(Run("r-1", "alpha", "1002", T));

        var record = (await ReadAsync()).Tickets.Last();

        record.Start!.State.Should().Be(FiledStartState.Record);
        record.Runs.Should().BeEmpty("nothing routes a record and no run works it");
    }

    [Fact]
    public async Task FiledWork_RunInAnotherProjectOnTheSameTracker_IsShown()
    {
        await FilingAsync(Filed(Work, "alpha"));
        await RunsAsync(Run("r-beta", "beta", Work, T));

        (await ReadAsync()).Tickets.Single().Runs.Select(r => r.Project).Should().Equal("beta");
    }

    [Fact]
    public async Task FiledWork_ReachSpansAnUnreadableProject_ShowsNoRunsFromIt()
    {
        await FilingAsync(Filed(Work, "alpha"));
        await RunsAsync(Run("r-alpha", "alpha", Work, T), Run("r-beta", "beta", Work, T));

        var ticket = (await ReadAsync(AgentSmith.Infrastructure.Persistence.Contracts.ProjectScope.Of(["alpha"]))).Tickets.Single();

        ticket.Runs.Select(r => r.Project).Should().Equal("alpha");
        ticket.Project.Should().Be("alpha", "the conversation's own filing stays");
    }

    [Fact]
    public async Task FiledWork_RunInAProjectOnAnotherTracker_IsNotShown()
    {
        await FilingAsync(Filed(Work, "alpha"));
        await RunsAsync(Run("r-far", "gamma", Work, T));

        (await ReadAsync()).Tickets.Single().Runs
            .Should().BeEmpty("a ticket id means nothing across two trackers");
    }

    [Fact]
    public async Task FiledWork_TicketWithoutARun_ShowsTheTicketAndNoRun()
    {
        await FilingAsync(Filed(Work, "alpha"));

        var ticket = (await ReadAsync()).Tickets.Single();

        ticket.TicketId.Should().Be(Work);
        ticket.Runs.Should().BeEmpty();
    }

    [Fact]
    public async Task FiledWork_FilingRecordWithoutIds_ShowsReferencesOnly()
    {
        await FilingAsync(new FiledTicket("https://tracker.test/9", "An older filing"));

        var ticket = (await ReadAsync()).Tickets.Single();

        ticket.Reference.Should().Be("https://tracker.test/9");
        ticket.TicketId.Should().BeNull();
        ticket.Runs.Should().BeEmpty();
    }

    [Fact]
    public async Task FiledWork_WaitingRun_CarriesItsPendingQuestion()
    {
        await FilingAsync(Filed(Work, "alpha"));
        await RunsAsync(Run("r-1", "alpha", Work, T, status: RunStatuses.WaitingForInput));
        await AddAsync(Checkpoint(JsonSerializer.Serialize(new DialogQuestion(
            "q-1", QuestionType.FreeText, "Which base branch?", null, null, null,
            TimeSpan.FromHours(4)))));

        var run = (await ReadAsync()).Tickets.Single().Runs.Single();

        run.PendingQuestion!.Text.Should().Be("Which base branch?");
    }

    [Fact]
    public async Task FiledWork_HandedBackTicket_CarriesTheCase()
    {
        await FilingAsync(Filed(Work, "alpha"));
        await AddAsync(SpecSet("alpha", SpecHandbackCase.Question, 2));

        var handback = (await ReadAsync()).Tickets.Single().Handback;

        handback!.Case.Should().Be(nameof(SpecHandbackCase.Question));
        handback.Repeated.Should().Be(2);
    }

    /// <summary>
    /// The producer of a <c>failed</c> row is VerifySpecHandler, whose verdict is the failing
    /// command. The read carries the verdict AND the status verbatim, because the status is
    /// what tells a red build from a false premise — the verdict text never is.
    /// </summary>
    [Fact]
    public async Task FiledWork_SpecFailedByARedBuild_CarriesItsVerdictAndSaysItFailed()
    {
        await FilingAsync(Filed(Work, "alpha"));
        await RunsAsync(Run("r-1", "alpha", Work, T));
        await SpecsAsync(Spec("r-1", "p1", 1, "failed", "dotnet test exited 1"));

        var spec = (await ReadAsync()).Tickets.Single().Runs.Single().Specs.Single();

        spec.Verdict.Should().Be("dotnet test exited 1");
        spec.Status.Should().Be("failed");
    }

    /// <summary>
    /// 2026-09-17-0e79c's own status. It is TERMINAL, so the premise, the finding and the
    /// evidence it recorded as the verdict reach the operator who has to amend the set.
    /// </summary>
    [Fact]
    public async Task FiledWork_HandedBackSpec_IsTerminalAndCarriesThePremiseItRecorded()
    {
        await FilingAsync(Filed(Work, "alpha"));
        await RunsAsync(Run("r-1", "alpha", Work, T));
        await SpecsAsync(
            Spec("r-1", "p1", 1, "handed_back", "the premise 'api has no cache' is false (P3)"));

        var spec = (await ReadAsync()).Tickets.Single().Runs.Single().Specs.Single();

        spec.Status.Should().Be("handed_back");
        spec.Verdict.Should().Be("the premise 'api has no cache' is false (P3)");
    }

    /// <summary>
    /// A state this reader was never taught shows its verdict rather than hiding it: terminal
    /// is stated as "not running", so the next status appended upstream is not silently read
    /// as a spec still going.
    /// </summary>
    [Fact]
    public async Task FiledWork_SpecInAStatusThisReadDoesNotKnow_StillShowsItsVerdict()
    {
        await FilingAsync(Filed(Work, "alpha"));
        await RunsAsync(Run("r-1", "alpha", Work, T));
        await SpecsAsync(Spec("r-1", "p1", 1, "abandoned", "the sandbox vanished"));

        (await ReadAsync()).Tickets.Single().Runs.Single().Specs.Single()
            .Verdict.Should().Be("the sandbox vanished");
    }

    [Fact]
    public async Task FiledWork_SpecVerdict_IsReadNeverInferred()
    {
        await FilingAsync(Filed(Work, "alpha"));
        await RunsAsync(Run("r-1", "alpha", Work, T));
        await SpecsAsync(Spec("r-1", "p1", 1, "done", "already satisfied on entry"));

        var spec = (await ReadAsync()).Tickets.Single().Runs.Single().Specs.Single();

        spec.Verdict.Should().Be("already satisfied on entry", "the row states it; nothing derives it");
    }

    [Fact]
    public async Task FiledWork_SpecRerunAfterAnAmendment_DoesNotShowTheOldVerdict()
    {
        await FilingAsync(Filed(Work, "alpha"));
        await RunsAsync(Run("r-1", "alpha", Work, T));
        // What the projection leaves behind: the verdict is kept, the status moved on.
        await SpecsAsync(Spec("r-1", "p1", 1, "in_progress", "the premise was false"));

        var spec = (await ReadAsync()).Tickets.Single().Runs.Single().Specs.Single();

        spec.Verdict.Should().BeNull("a verdict belongs to a terminal row, not to a running one");
    }

    [Fact]
    public async Task FiledWork_RunningSpec_ShowsNoVerdict()
    {
        await FilingAsync(Filed(Work, "alpha"));
        await RunsAsync(Run("r-1", "alpha", Work, T));
        await SpecsAsync(Spec("r-1", "p1", 1, "in_progress"));

        var spec = (await ReadAsync()).Tickets.Single().Runs.Single().Specs.Single();

        spec.Status.Should().Be("in_progress", "a running spec is shown as running");
        spec.Verdict.Should().BeNull();
    }

    [Fact]
    public async Task FiledWork_SpecReviewArtifact_CarriesItsFindings()
    {
        await ReviewedAsync(
            """
            {"reviewed":true,"findings":[{"repository":"api","path":"src/A.cs","line":4,
            "rule":"no silent catch","why":"the catch body logs nothing","cites":"P1","reverted":null}]}
            """);

        var review = (await ReadAsync()).Tickets.Single().Runs.Single().Specs.Single().Review;

        review!.Reviewed.Should().BeTrue();
        review.Why.Should().BeNull();
        review.Findings.Single().Path.Should().Be("src/A.cs");
        review.Findings.Single().Why.Should().Be("the catch body logs nothing");
    }

    /// <summary>
    /// The distinction the artifact exists for: nobody was asked, and the reason travels. Read
    /// as a clean review this is the page reporting "nothing found" over a question nobody put.
    /// </summary>
    [Fact]
    public async Task FiledWork_SpecReviewNotTaken_SaysSoWithItsReason()
    {
        await ReviewedAsync(
            """{"reviewed":false,"findings":[],"why":"the run's configured cost cap is exhausted"}""");

        var review = (await ReadAsync()).Tickets.Single().Runs.Single().Specs.Single().Review;

        review!.Reviewed.Should().BeFalse();
        review.Why.Should().Be("the run's configured cost cap is exhausted");
        review.Findings.Should().BeEmpty();
    }

    /// <summary>A spec that never reached its review has no row, which is a third state again.</summary>
    [Fact]
    public async Task FiledWork_SpecWithoutAReviewRow_CarriesNoReview()
    {
        await FilingAsync(Filed(Work, "alpha"));
        await RunsAsync(Run("r-1", "alpha", Work, T));
        await SpecsAsync(Spec("r-1", "p1", 1, "in_progress"));

        (await ReadAsync()).Tickets.Single().Runs.Single().Specs.Single()
            .Review.Should().BeNull();
    }

    /// <summary>
    /// A ticket may carry a spec set in two projects of one tracker — an old uncleared case in
    /// one and this morning's in the other. The row written LAST is the standing case, and it
    /// is deliberately the one inserted FIRST here: neither insertion order nor its reverse
    /// can answer this, only when each row was written.
    /// </summary>
    [Fact]
    public async Task FiledWork_HandbackInTwoProjectsOfOneTracker_NamesTheOneWrittenLast()
    {
        await FilingAsync(Filed(Work, "alpha"));
        await AddAsync(SpecSet("alpha", SpecHandbackCase.RequirementsContradictRepository, 1));
        await AddAsync(SpecSet("beta", SpecHandbackCase.Question, 3));
        await TouchAsync("alpha", repeated: 2);

        var handback = (await ReadAsync()).Tickets.Single().Handback;

        handback!.Case.Should().Be(nameof(SpecHandbackCase.RequirementsContradictRepository));
        handback.Repeated.Should().Be(2);
    }

    /// <summary>Writes the project's spec set again, so UpdatedAt is stamped afresh.</summary>
    private async Task TouchAsync(string project, int repeated)
    {
        await using var ctx = new AgentSmithDbContext(Options());
        var row = ctx.Set<TicketSeries>().Single(r => r.Project == project);
        row.RepeatedHandbackCount = repeated;
        await ctx.SaveChangesAsync();
    }

    /// <summary>
    /// A truncated row, or a payload written before the report became an object, must not read
    /// as a spec nobody reviewed — that is 042eh's collapse, one state over.
    /// </summary>
    [Fact]
    public async Task FiledWork_UnreadableSpecReview_SaysSoRatherThanReadingAsAbsent()
    {
        await ReviewedAsync("[{\"repository\":\"api\",\"path\":\"src/A.cs\"}");

        var review = (await ReadAsync()).Tickets.Single().Runs.Single().Specs.Single().Review;

        review.Should().NotBeNull("a row that exists is not a spec that was never reviewed");
        review!.Unreadable.Should().BeTrue();
        review.Why.Should().Be(FiledWorkReviewView.CouldNotBeRead);
    }

    /// <summary>
    /// SpecReviewReport.None is publishable, so "why is non-null exactly when not reviewed"
    /// is the producer's intent and not a guarantee. The reader states a reason either way,
    /// and findings recorded beside a not-taken review are still carried.
    /// </summary>
    [Fact]
    public async Task FiledWork_SpecReviewNotTakenWithoutAReason_StillSaysItWasNotTaken()
    {
        await ReviewedAsync(
            """
            {"reviewed":false,"findings":[{"repository":"api","path":"src/A.cs","line":4,
            "rule":"r","why":"kept from an earlier pass"}]}
            """);

        var review = (await ReadAsync()).Tickets.Single().Runs.Single().Specs.Single().Review;

        review!.Reviewed.Should().BeFalse();
        review.Unreadable.Should().BeFalse();
        review.Why.Should().Be(FiledWorkReviewView.NoReasonRecorded);
        review.Findings.Should().ContainSingle("a report may carry both");
    }

    /// <summary>
    /// The wire contract between 2026-09-17-042eh and this read, taken through the REAL
    /// publisher: whatever it serializes is what this reader deserializes.
    /// </summary>
    [Fact]
    public async Task FiledWork_SpecReviewWrittenByThePublisher_IsReadBackAsItWasRecorded()
    {
        var recorded = SpecReviewReport.Taken(
            [new SpecFinding("api", "src/A.cs", 4, "no silent catch", "logs nothing", "P1", "reverted")]);
        var events = new RecordingEventPublisher();
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "r-1");
        await new SpecReviewPublisher(events).PublishAsync(pipeline, "p1", recorded, default);

        await ReviewedAsync(events.Published.OfType<SpecReviewedEvent>().Single().ReportJson);

        var review = (await ReadAsync()).Tickets.Single().Runs.Single().Specs.Single().Review;
        review!.Reviewed.Should().BeTrue();
        review.Findings.Should().BeEquivalentTo(recorded.Findings);
    }

    [Fact]
    public async Task FiledWork_TemplateFinding_KeepsItsKind()
    {
        var recorded = SpecReviewReport.Taken(
            [new SpecFinding("api", "src/A.cs", 4, "handlers live under Features/", "in Api/", "P1",
                Kind: SpecFinding.TemplateKind)]);
        var events = new RecordingEventPublisher();
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "r-1");
        await new SpecReviewPublisher(events).PublishAsync(pipeline, "p1", recorded, default);

        var review = new FiledWorkSpecReviews(NullLogger<FiledWorkSpecReviews>.Instance)
            .Of(events.Published.OfType<SpecReviewedEvent>().Single().ReportJson, "p1");

        review!.Findings.Single().IsTemplate.Should().BeTrue("the page shows a template deviation as advice");
    }

    /// <summary>
    /// One malformed parked question used to take the whole read down — and with it every
    /// ticket of the conversation, over one run nobody was looking at.
    /// </summary>
    [Fact]
    public async Task FiledWork_UnreadableParkedQuestion_ReadsAsAbsent()
    {
        await FilingAsync(Filed(Work, "alpha"));
        await RunsAsync(Run("r-1", "alpha", Work, T, status: RunStatuses.WaitingForInput));
        await AddAsync(Checkpoint("{\"questionId\": "));

        var run = (await ReadAsync()).Tickets.Single().Runs.Single();

        run.PendingQuestion.Should().BeNull();
        run.RunId.Should().Be("r-1", "the rest of the conversation's work still reads");
    }

    [Fact]
    public async Task FiledWork_DialogIdWithNoOpenSession_IsEmpty()
    {
        await RunsAsync(Run("r-1", "alpha", Work, T));

        (await ReadAsync()).Tickets.Should().BeEmpty();
    }

    [Fact]
    public async Task FiledWork_RunPullRequests_AreTheOnesTheRunRecorded()
    {
        await FilingAsync(Filed(Work, "alpha"));
        await RunsAsync(Run("r-1", "alpha", Work, T, pullRequests:
            """[{"repo":"api","status":"opened","url":"https://git.test/pr/1","openedAt":"2026-09-17T10:00:00Z"}]"""));

        (await ReadAsync()).Tickets.Single().Runs.Single().PullRequests
            .Single().Url.Should().Be("https://git.test/pr/1");
    }

    /// <summary>
    /// 2026-09-25-c4a6: the conversation filed nothing and is BOUND to the ticket, so the runs
    /// are reached through the binding. The row is a filing's shape with no filing behind it: its
    /// identity is the tracker's id, because a reference is the url a created ticket carried.
    /// </summary>
    [Fact]
    public async Task TicketRuns_TheRunsForATicketNobodyFiled_AreFoundAndShown()
    {
        await BoundAsync();
        await RunsAsync(Run("r-1", "alpha", Work, T, pullRequests:
            """[{"repo":"api","status":"opened","url":"https://git.test/pr/1","openedAt":"2026-09-17T10:00:00Z"}]"""));

        var ticket = (await ReadAsync()).Tickets.Single();

        ticket.TicketId.Should().Be(Work);
        ticket.Reference.Should().Be(Work, "a reference is a created ticket's url and nobody created this one here");
        ticket.Title.Should().Be($"Work {Work}");
        ticket.Start!.State.Should().Be(FiledStartState.NotFiled);
        ticket.Runs.Single().RunId.Should().Be("r-1");
        ticket.Runs.Single().PullRequests.Single().Url.Should().Be("https://git.test/pr/1");
    }

    [Fact]
    public async Task TicketRuns_TheReach_IsTheProjectsSharingTheConversationsTracker()
    {
        await BoundAsync();
        await RunsAsync(Run("r-beta", "beta", Work, T), Run("r-gamma", "gamma", Work, T));

        (await ReadAsync()).Tickets.Single().Runs.Select(r => r.Project).Should()
            .Equal(["beta"], "a bare ticket number means different work on two trackers");
    }

    /// <summary>
    /// The binding is the key only where there is no filing. A conversation that filed is shown
    /// what IT filed — a second row for the ticket it belongs to would be the same work twice.
    /// </summary>
    [Fact]
    public async Task TicketRuns_AConversationWithAFiling_BehavesExactlyAsBefore()
    {
        await BoundAsync("2002");
        await FileAsync(Filed(Work, "alpha"));
        await RunsAsync(Run("r-1", "alpha", Work, T), Run("r-2", "alpha", "2002", T));

        var ticket = (await ReadAsync()).Tickets.Single();

        ticket.Reference.Should().Be($"https://tracker.test/{Work}");
        ticket.Start!.State.Should().Be(FiledStartState.Started);
        ticket.Runs.Single().RunId.Should().Be("r-1");
    }

    /// <summary>
    /// The spec key cannot be turned back into a tracker id, so a conversation whose ticket was
    /// never read is bound to work nothing can find. It shows no row rather than a guessed id,
    /// which would match another ticket's runs.
    /// </summary>
    [Fact]
    public async Task TicketRuns_ABoundTicketWhoseTextWasNeverRead_ShowsNoRow()
    {
        await AddAsync(new SpecDialogSession
        {
            SessionId = "s-1", Platform = Platform, ChannelId = Dialog, ThreadId = Dialog,
            UserId = Owner, Project = "alpha", IsOpen = true, LastActivityAt = T,
            Tracker = "atlas", TicketKey = TicketKey.For("jira", Work).Value,
        });
        await RunsAsync(Run("r-1", "alpha", Work, T));

        (await ReadAsync()).Tickets.Should().BeEmpty();
    }

    private static TicketSeries SpecSet(string project, SpecHandbackCase handback, int repeated) =>
        new()
        {
            Project = project,
            TicketKey = TicketKey.For("jira", Work).Value,
            CarryingRepo = "api",
            RevisionSha = "abc",
            LastHandbackCase = (int)handback,
            RepeatedHandbackCount = repeated,
        };

    private static RunCheckpoint Checkpoint(string questionJson) => new()
    {
        RunId = "r-1", Project = "alpha", TicketId = Work, Pipeline = "code",
        DialogueJobId = "j", QuestionId = "q-1", AskedAt = T, AnswerDeadlineAt = T.AddHours(4),
        QuestionJson = questionJson, RemainingCommandsJson = "[]", ContextJson = "{}",
    };

    /// <summary>Captures what the real publisher emits, so the wire shape is not hand-written.</summary>
    private sealed class RecordingEventPublisher : IEventPublisher
    {
        public List<Contracts.Events.RunEvent> Published { get; } = [];

        public Task PublishAsync(
            Contracts.Events.RunEvent runEvent, CancellationToken cancellationToken = default)
        {
            Published.Add(runEvent);
            return Task.CompletedTask;
        }
    }

    private async Task ReviewedAsync(string reviewJson, string kindPrefix = RunSpecProjection.ReviewKindPrefix)
    {
        await FilingAsync(Filed(Work, "alpha"));
        await RunsAsync(Run("r-1", "alpha", Work, T));
        await SpecsAsync(Spec("r-1", "p1", 1, "done"));
        await AddAsync(new RunArtifact
        {
            RunId = "r-1",
            Kind = kindPrefix + "p1",
            Content = reviewJson,
        });
    }

    // Two projects on one tracker and a third on another: the reach of a filed ticket is the
    // tracker's, because a ticket id is unique within one and means nothing across two.
    private static AgentSmithConfig Config() => new()
    {
        Projects = new Dictionary<string, ResolvedProject>(ConfigNames.Comparer)
        {
            ["alpha"] = Project("alpha", "atlas"),
            ["beta"] = Project("beta", "atlas"),
            ["gamma"] = Project("gamma", "other"),
        },
    };

    private static ResolvedProject Project(string name, string tracker) => new()
    {
        Name = name,
        Tracker = new TrackerConnection { Name = tracker, Type = TrackerType.Jira },
    };

    private static FiledTicket Filed(string ticketId, string project) =>
        new($"https://tracker.test/{ticketId}", $"Work {ticketId}")
        {
            TicketId = ticketId,
            Project = project,
            Key = $"KEY-{ticketId}",
            Start = new FiledWorkStart(FiledStartState.Started, "it already triggers"),
        };

    private static Run Run(
        string id, string project, string ticketId, DateTimeOffset startedAt,
        string status = "success", string? pullRequests = null) =>
        new()
        {
            Id = id, Project = project, TicketId = ticketId, Pipeline = "code",
            Status = status, StartedAt = startedAt, CostTotalUsd = 1.25m,
            PullRequestsJson = pullRequests,
        };

    private static RunSpec Spec(
        string runId, string specId, int ordinal, string status, string? verdict = null) =>
        new()
        {
            RunId = runId, SpecId = specId, Ordinal = ordinal, Status = status,
            Title = $"Spec {specId}", StartedAt = T, Verdict = verdict,
        };

    private async Task FilingAsync(params FiledTicket[] filed)
    {
        await AddAsync(new SpecDialogSession
        {
            SessionId = "s-1", Platform = Platform, ChannelId = Dialog, ThreadId = Dialog,
            UserId = Owner, Project = "alpha", IsOpen = true, LastActivityAt = T,
        });
        await FileAsync(filed);
    }

    /// <summary>Writes the latest filing onto whatever open session this dialog already has.</summary>
    private async Task FileAsync(params FiledTicket[] filed)
    {
        await using var ctx = new AgentSmithDbContext(Options());
        var store = new SpecDialogLatestOutcomeStore(
            new SpecDialogSessionRepository(ctx), AgentSmith.Tests.TestSupport.BoundSessionNudges.Quiet(new SpecDialogSessionRepository(ctx)), NullLogger<SpecDialogLatestOutcomeStore>.Instance);
        await store.SetFilingAsync(
            Platform, Dialog, new FilingReport(filed, null), new AnswerOutcome(),
            CancellationToken.None);
    }

    /// <summary>
    /// 2026-09-25-c4a6: a conversation BOUND to a ticket (2026-09-25-8e51b) that filed nothing.
    /// The session row keeps the tracker connection and the SPEC KEY spelling; the ticket text
    /// the binding read (2026-09-25-8e51c) is the only place the tracker's own id is kept.
    /// </summary>
    private async Task BoundAsync(string ticketId = Work, string project = "alpha")
    {
        await AddAsync(new SpecDialogSession
        {
            SessionId = "s-1", Platform = Platform, ChannelId = Dialog, ThreadId = Dialog,
            UserId = Owner, Project = project, IsOpen = true, LastActivityAt = T,
            Tracker = "atlas", TicketKey = TicketKey.For("jira", ticketId).Value,
        });
        await AddAsync(new SpecDialogTicketText
        {
            SessionId = "s-1", TicketId = ticketId, Title = $"Work {ticketId}",
            Text = "what the ticket says", Fingerprint = "f", ReadAt = T,
        });
    }

    private Task RunsAsync(params Run[] runs) => AddAsync(runs);
    private Task SpecsAsync(params RunSpec[] specs) => AddAsync(specs);

    private async Task AddAsync<T>(params T[] rows) where T : class
    {
        await using var ctx = new AgentSmithDbContext(Options());
        foreach (var row in rows) ctx.Add(row);
        await ctx.SaveChangesAsync();
    }

    private Task<FiledWorkView> ReadAsync(AgentSmith.Infrastructure.Persistence.Contracts.ProjectScope? scope = null) =>
        Reader().ReadAsync(Dialog, scope ?? AgentSmith.Infrastructure.Persistence.Contracts.ProjectScope.Everything, CancellationToken.None);

    private FiledWorkReader Reader()
    {
        var ctx = new AgentSmithDbContext(Options());
        return new FiledWorkReader(
            new FiledWorkFiling(new SpecDialogLatestOutcomeStore(
                new SpecDialogSessionRepository(ctx), AgentSmith.Tests.TestSupport.BoundSessionNudges.Quiet(new SpecDialogSessionRepository(ctx)),
                NullLogger<SpecDialogLatestOutcomeStore>.Instance)),
            new FiledWorkBoundTicket(
                new SpecDialogSessionRepository(ctx), new SpecDialogTicketTextRepository(ctx)),
            new FiledWorkTrackerProjects(Config()),
            new FiledWorkRunsReader(
                _scopes, new FiledWorkSpecReviews(NullLogger<FiledWorkSpecReviews>.Instance),
                new DbRunCheckpointStore(_scopes), NullLogger<FiledWorkRunsReader>.Instance),
            new FiledWorkHandbacks(_scopes),
            // 2026-09-25-8e51d: these cases are about FILED work; an unbound conversation has no
            // approved set to show, and the reader asks for one either way.
            new ApprovedSetForConversation(
                new SpecDialogSessionRepository(ctx),
                new AgentSmith.Application.Services.Persistence.InMemorySpecApprovalStore()));
    }

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;
}
