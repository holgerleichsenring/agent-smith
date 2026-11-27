using AgentSmith.Application.Services.Holds;
using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Runs;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Tickets.Holds;

/// <summary>2026-11-23-31dcg: one hold per ticket from its stored facts — one case per addressee, and when several sources
/// apply the most blocking wins: admin over decide over plan over merge over nobody.</summary>
public sealed class TicketHoldReaderTests
{
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-11-23T09:00:00Z");
    private readonly TicketHoldReader _reader = new();

    private static TicketHoldFacts Facts(string? status = null) =>
        new("alpha", "42") { HeadStatus = status, HeadRunId = "r-2", Attempt = 2, EndedAt = T };

    private static SeriesRunMergeStep Step(string wait, params string[] after) => new("42", "SCRUM-42", 1, wait, after);

    [Fact]
    public void Admin_AnUntrustedImage_OpensTheSandboxSettings()
    {
        var hold = _reader.Of(Facts(RunStatuses.Failed) with { EndKind = RunEndKinds.ImageUntrusted })!;

        hold.Should().BeEquivalentTo(new TicketHold(TicketHoldAddressees.Admin, TicketHoldCases.ImageUntrusted,
            new TicketHoldAct(TicketHoldActs.Settings, TicketHoldReader.SandboxSettingsPath), T) { Attempt = 2 });
        _reader.Of(Facts(RunStatuses.Failed) with { EndKind = RunEndKinds.BootstrapMissing })!.Act
            .Should().Be(new TicketHoldAct(TicketHoldActs.Init, "alpha"));
        var detail = new EndDetail("The pipeline's agent", [], "agents.<name>.model", "a model", "/config/agents");
        var preflight = _reader.Of(Facts(RunStatuses.Failed) with { EndKind = RunEndKinds.Preflight, EndDetail = detail })!;
        preflight.Act.Should().Be(new TicketHoldAct(TicketHoldActs.Settings, "/config/agents"));
        preflight.Detail.Should().Be(detail, "the sentence quotes what the stop recorded");
    }

    [Fact]
    public void Decide_AParkedQuestionOrHandback_AFailureAndAShortfall()
    {
        _reader.Of(Facts(RunStatuses.WaitingForInput) with { Parked = true, Asked = true, ParkedAt = T })!
            .Should().Match<TicketHold>(h => h.Case == TicketHoldCases.Question && h.Act!.Kind == TicketHoldActs.Answer && h.Since == T);
        _reader.Of(Facts(RunStatuses.WaitingForInput) with { Parked = true })!.Case.Should().Be(TicketHoldCases.Handback);
        _reader.Of(Facts(RunStatuses.Failed))!.Should().Match<TicketHold>(h =>
            h.Addressee == TicketHoldAddressees.Decide && h.Case == TicketHoldCases.Failed && h.Act!.Kind == TicketHoldActs.Conversation);
        _reader.Of(Facts(RunStatuses.Failed) with { EndedOnInfrastructure = true })!.Act
            .Should().Be(new TicketHoldAct(TicketHoldActs.Retry, "r-2"));
        _reader.Of(Facts(RunStatuses.Failed) with { EndKind = RunEndKinds.ReadinessUnknown })!.Case.Should().Be(TicketHoldCases.ReadinessUnknown);
        _reader.Of(Facts(RunStatuses.Failed) with { EndKind = RunEndKinds.CriteriaUnmet })!.Case.Should().Be(TicketHoldCases.CriteriaUnmet);
        _reader.Of(Facts(RunStatuses.Shortfall))!.Case.Should().Be(TicketHoldCases.Shortfall);
    }

    [Fact]
    public void Decide_AnInterruptionTheRestartTakesUp_IsNobodys_OneItDoesNot_IsRetried()
    {
        _reader.Of(Facts("cancelled") with { Interrupted = true, Restarting = true })!
            .Should().Match<TicketHold>(h => h.Addressee == TicketHoldAddressees.Nobody && h.Case == TicketHoldCases.Restarting);
        _reader.Of(Facts("cancelled") with { Interrupted = true })!
            .Should().Match<TicketHold>(h => h.Case == TicketHoldCases.Interrupted && h.Act!.Kind == TicketHoldActs.Retry);
        _reader.Of(Facts("cancelled")).Should().BeNull("a person's cancel holds nothing");
    }

    [Fact]
    public void Plan_AnEditedTextTheConversationHasNotAnswered()
    {
        _reader.Of(Facts(RunStatuses.Success) with { TextEdited = true })!
            .Should().Match<TicketHold>(h => h.Addressee == TicketHoldAddressees.Plan && h.Act!.Kind == TicketHoldActs.Conversation);
    }

    [Fact]
    public void Merge_TheNextInTheSeriesOrder()
    {
        _reader.Of(Facts(RunStatuses.Success) with { SeriesId = "sr-1", MergeStep = Step(SeriesRunMergeWaits.Ready) })!
            .Should().BeEquivalentTo(new TicketHold(TicketHoldAddressees.Merge, TicketHoldCases.MergeNext,
                new TicketHoldAct(TicketHoldActs.Merge, "sr-1"), T) { Attempt = 2 });
    }

    // 2026-11-23-849e: the series' one merge hold names the step after it and what waits on merges to start, and whether
    // merging approves; a ticket that starts only on merges waits on nobody for them, the series' next first.
    private static readonly SeriesRunMergeStep[] Order =
    [
        new("42", "#42", 0, SeriesRunMergeWaits.Ready, []), new("44", "#44", 1, SeriesRunMergeWaits.After, ["#42"]),
        new("45", "#45", 2, SeriesRunMergeWaits.After, ["#44"]),
    ];

    [Fact]
    public void Merge_TheSeriesNextMerge_NamesWhatItUnblocksAndTheNextStep()
    {
        var hold = _reader.Of(Facts(RunStatuses.Success) with
        {
            SeriesId = "sr-1", MergeStep = Order[0], SeriesMergeOrder = Order, SeriesStartsOnMerge = ["#50", "#51"],
        })!;

        (hold.Addressee, hold.Case, hold.Next).Should().Be((TicketHoldAddressees.Merge, TicketHoldCases.MergeNext, "#44"));
        hold.Names.Should().Equal("#50", "#51");
    }

    [Fact]
    public void Nobody_ANeverRunTicketThatStartsOnMerges_WaitsForThemWithTheSeriesNextFirst()
    {
        var hold = _reader.Of(new TicketHoldFacts("alpha", "50")
        {
            SeriesId = "sr-1", SeriesMergeOrder = Order, StartsOnceMerged = ["#45"], FiledAt = T,
        })!;

        (hold.Addressee, hold.Case, hold.Merges, hold.Next, hold.Since).Should()
            .Be((TicketHoldAddressees.Nobody, TicketHoldCases.StartsOnMerge, (int?)3, "#42", (DateTimeOffset?)T));
        hold.Act.Should().Be(new TicketHoldAct(TicketHoldActs.Series, "sr-1"));
        hold.Names.Should().Equal("#45");
        _reader.Of(new TicketHoldFacts("alpha", "51") { StartsOnceMerged = ["#60"] })!
            .Should().Match<TicketHold>(h => h.Merges == 1 && h.Next == "#60" && h.Act == null, "outside any series' order it waits for its own merges");
        _reader.Of(new TicketHoldFacts("alpha", "52") { SeriesId = "sr-1", SeriesMergeOrder = Order, StartsOnceMerged = ["#61"] })!
            .Should().Match<TicketHold>(h => h.Merges == 1 && h.Next == "#61", "a merge it needs has no pull request in the order yet");
    }

    [Fact]
    public void Escalation_ACiRunningLongerThanTheMinutes_IsTheAdmins()
    {
        var open = new TicketOpenPullRequests(AgentSmith.Contracts.Reviews.CiStates.Pending, T);
        var facts = Facts(RunStatuses.Success) with { OpenPullRequests = open, EscalateAfterMinutes = 60 };

        _reader.Of(facts with { Now = T.AddHours(5) })!.Should().Match<TicketHold>(h =>
            h.Addressee == TicketHoldAddressees.Admin && h.Case == TicketHoldCases.CiRunning && h.Escalation!.AfterMinutes == 60);
        _reader.Of(facts with { Now = T.AddMinutes(30) })!.Addressee.Should().Be(TicketHoldAddressees.Nobody);
    }

    [Fact]
    public void Nobody_WorkQueueResumeMergeAfterRetargetAndARunningPrerequisite()
    {
        _reader.Of(Facts(RunStatuses.Running))!.Case.Should().Be(TicketHoldCases.Working);
        _reader.Of(Facts(RunStatuses.Queued))!.Case.Should().Be(TicketHoldCases.Queued);
        _reader.Of(Facts(RunStatuses.WaitingForInput))!.Case.Should().Be(TicketHoldCases.Resuming);
        _reader.Of(Facts(RunStatuses.Success) with { SeriesId = "sr-1", MergeStep = Step(SeriesRunMergeWaits.After, "SCRUM-41") })!
            .Should().Match<TicketHold>(h => h.Case == TicketHoldCases.MergeAfter && h.Names.Single() == "SCRUM-41" && h.Act == null);
        _reader.Of(Facts(RunStatuses.Success) with { SeriesId = "sr-1", MergeStep = Step(SeriesRunMergeWaits.Retarget) })!
            .Case.Should().Be(TicketHoldCases.Retarget);
        _reader.Of(new TicketHoldFacts("alpha", "43") { WaitsFor = ["#42"] })!
            .Should().Match<TicketHold>(h => h.Addressee == TicketHoldAddressees.Nobody && h.Case == TicketHoldCases.Prerequisite);
    }

    [Fact]
    public void NothingHolds_AFinishedTicket_OrOneNothingWaitsOn()
    {
        _reader.Of(Facts(RunStatuses.Failed) with { Finished = true, TextEdited = true }).Should().BeNull();
        _reader.Of(Facts(RunStatuses.Success)).Should().BeNull();
        _reader.Of(new TicketHoldFacts("alpha", "43")).Should().BeNull("a filed ticket starts when it is started");
    }

    [Fact]
    public void Precedence_AdminOverDecide()
    {
        // A parked question beside a precondition stop cannot both be the newest attempt; the facts say both all the same.
        var facts = Facts(RunStatuses.Failed) with { EndKind = RunEndKinds.Preflight, Parked = true, Asked = true };
        _reader.Decide(facts).Should().NotBeNull();
        _reader.Of(facts)!.Addressee.Should().Be(TicketHoldAddressees.Admin);
    }

    [Fact]
    public void Precedence_DecideOverPlan()
    {
        _reader.Of(Facts(RunStatuses.Failed) with { TextEdited = true })!.Addressee.Should().Be(TicketHoldAddressees.Decide);
    }

    [Fact]
    public void Precedence_PlanOverMerge()
    {
        var facts = Facts(RunStatuses.Success) with { TextEdited = true, SeriesId = "sr-1", MergeStep = Step(SeriesRunMergeWaits.Ready) };
        _reader.Merge(facts).Should().NotBeNull();
        _reader.Of(facts)!.Addressee.Should().Be(TicketHoldAddressees.Plan);
    }

    [Fact]
    public void Precedence_MergeOverNobody()
    {
        var facts = Facts(RunStatuses.Success) with { SeriesId = "sr-1", MergeStep = Step(SeriesRunMergeWaits.Ready), WaitsFor = ["#41"] };
        _reader.Nobody(facts).Should().NotBeNull();
        _reader.Of(facts)!.Addressee.Should().Be(TicketHoldAddressees.Merge);
        // 2026-11-23-849e: a start on merges is nobody's, ahead of a requirement still worked.
        _reader.Of(new TicketHoldFacts("alpha", "43") { StartsOnceMerged = ["#42"], WaitsFor = ["#41"] })!.Case
            .Should().Be(TicketHoldCases.StartsOnMerge);
    }

    [Fact]
    public void Merge_ATicketAtWork_IsNotHeldByItsPullRequest()
    {
        _reader.Of(Facts(RunStatuses.Running) with { SeriesId = "sr-1", MergeStep = Step(SeriesRunMergeWaits.Ready) })!
            .Case.Should().Be(TicketHoldCases.Working);
    }

    [Fact]
    public void IsPersons_MergePlanDecide_NotAdminOrNobody()
    {
        new[] { TicketHoldAddressees.Merge, TicketHoldAddressees.Plan, TicketHoldAddressees.Decide, TicketHoldAddressees.Admin, TicketHoldAddressees.Nobody }
            .Select(a => new TicketHold(a, "x", null, null).IsPersons).Should().Equal(true, true, true, false, false);
    }

    // 2026-11-23-31dch: a parked question holds by who can answer it.
    private static TicketHoldFacts AskedOf(string? series, params string[] answerers) =>
        Facts(RunStatuses.WaitingForInput) with
        {
            Parked = true, Asked = true, ParkedAt = T, SeriesId = series, SeriesActive = series is not null, Answerers = answerers,
        };

    // 2026-11-27-3532b: in a series that leads the ticket every parked question is its master's — one Question hold, the
    // series' conversation, addressed to nobody: no admin hold, no person's "needs you".
    private static readonly TicketHold MastersQuestion = new(TicketHoldAddressees.Nobody, TicketHoldCases.Question,
        new TicketHoldAct(TicketHoldActs.Conversation, "sr-1"), T) { Attempt = 2 };

    [Theory]
    [InlineData("target")]
    [InlineData("plan")]
    [InlineData("target", "plan")]
    public void Question_SeriesLed_IsTheMastersQuestion(params string[] answerers)
    {
        _reader.Of(AskedOf("sr-1", answerers)).Should().BeEquivalentTo(MastersQuestion);
    }

    // 2026-11-27-3532b: a master's question that waits too long is the admin's, like any wait on nobody.
    [Fact]
    public void Question_SeriesLed_WaitingTooLong_IsTheAdmins()
    {
        var hold = _reader.Of(AskedOf("sr-1", "target") with { EscalateAfterMinutes = 60, Now = T.AddHours(2) })!;

        hold.Addressee.Should().Be(TicketHoldAddressees.Admin);
        hold.Case.Should().Be(TicketHoldCases.Question);
        hold.Escalation.Should().NotBeNull();
    }

    [Fact]
    public void Environment_SeriesLed_IsTheMastersQuestion()
    {
        var hold = _reader.Of(AskedOf("sr-1", "plan", "environment"));

        hold.Should().BeEquivalentTo(MastersQuestion);
        hold!.IsPersons.Should().BeFalse();
        _reader.Admin(AskedOf("sr-1", "environment")).Should().BeNull("the admin holds list does not carry it");
    }

    [Fact]
    public void Question_ThePlansOutsideASeries_AndTheTargets_AreAnsweredOnTheTicket()
    {
        _reader.Of(AskedOf(null, "plan"))!.Should().Match<TicketHold>(h =>
            h.Addressee == TicketHoldAddressees.Decide && h.Case == TicketHoldCases.Question && h.Act!.Kind == TicketHoldActs.Answer);
        _reader.Of(AskedOf("sr-1", "plan") with { SeriesActive = false })!.Should().Match<TicketHold>(h =>
            h.Case == TicketHoldCases.Question && h.Act!.Kind == TicketHoldActs.Answer, "a series that ended reads no conversation");
    }

    [Fact]
    public void Question_TheEnvironments_IsReportedToTheAdmin_BeforeAnyOther()
    {
        var hold = _reader.Of(AskedOf(null, "plan", "environment"))!;

        hold.Should().Match<TicketHold>(h => h.Addressee == TicketHoldAddressees.Admin && h.Case == TicketHoldCases.QuestionEnvironment
            && h.Act!.Kind == TicketHoldActs.Answer && h.Since == T && h.Detail == null);
        _reader.Of(Facts(RunStatuses.Failed) with { Answerers = ["environment"] })!.Addressee
            .Should().Be(TicketHoldAddressees.Decide, "only a parked question is the environment's");
    }
}
