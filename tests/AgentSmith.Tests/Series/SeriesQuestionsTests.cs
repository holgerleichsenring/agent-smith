using AgentSmith.Application.Services.Series;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;
using AgentSmith.Domain.Entities;
using FluentAssertions;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-17-0207c: a series question with a recommendation is taken at its deadline and the conversation
/// says so; one without waits; an operator message answers first. 2026-11-27-3532b: a parked ticket's question is the
/// master's reason, once per run and question, never relayed into the chat; an answer to a question the master carried for
/// a ticket names that ticket.</summary>
public sealed class SeriesQuestionsTests
{
    private readonly InMemorySeriesRunStore _store = new();

    private SeriesQuestions Questions => new(_store, AgentSmith.Tests.TestSupport.SeriesWakeDoubles.Budget(_store), new DialogueGlobalConfig());

    private async Task<SeriesBeat> BeatAsync(SeriesQuestion? question, DateTimeOffset now, IReadOnlyList<SeriesTicketFact>? facts = null,
        SeriesRunRecord? with = null, params string[] messages)
    {
        var record = (with ?? SeriesRecords.Running()) with { Question = question };
        await _store.OpenAsync(record, default);
        var beat = new SeriesBeat(record, facts ?? [], now);
        beat.Messages.AddRange(messages.Select((m, i) => new SeriesOperatorMessage(10 + i, m, now)));
        await Questions.SettleAsync(beat, default);
        return beat;
    }

    private static SeriesQuestion Asked(int? recommended) =>
        SeriesQuestions.Ask("Keep the original deadline?", ["keep", "restart"], recommended, SeriesRecords.T, 120);

    private static SeriesTicketFact Parked(string ticket, string text, params string[] answerers) =>
        SeriesRecords.Fact(ticket, "waiting_for_input") with
        {
            DialogueJobId = $"run-{ticket}",
            PendingQuestion = new DialogQuestion($"q-{ticket}", QuestionType.Choice, text, null,
                [new DialogChoice("keep", "keep"), new DialogChoice("restart", "restart")], null, TimeSpan.FromDays(14), RecommendedIndex: 0)
            {
                Answerers = answerers,
            },
        };

    [Fact]
    public async Task RecommendedQuestion_PastItsDeadline_IsTakenByDefaultAndSaysWhen()
    {
        var beat = await BeatAsync(Asked(0), SeriesRecords.T.AddMinutes(121));

        var question = _store.Records["sr-1"].Question!;
        question.Answer.Should().Be("keep");
        question.ByDefault.Should().BeTrue();
        beat.Said.Should().ContainSingle().Which.Should().StartWith("Taken by default at 20:38 UTC: keep");
        beat.Reasons.Should().ContainSingle().Which.Should().Contain("answered by default: keep");
    }

    // 2026-11-28-d1ffb: dialogue.question_default_enabled on (as before) takes the configured minutes; the chat says when.
    [Fact]
    public void RecommendedQuestion_DefaultOn_TakesTheConfiguredMinutes()
    {
        var minutes = SeriesPolicy.QuestionDefaultMinutes(new DialogueGlobalConfig { QuestionDefaultMinutes = 30 });
        var asked = SeriesQuestions.Ask("Keep the original deadline?", ["keep", "restart"], 0, SeriesRecords.T, minutes);

        minutes.Should().Be(30);
        asked.DeadlineAt.Should().Be(SeriesRecords.T.AddMinutes(30));
        SeriesQuestionText.Asked(asked).Should().Contain($"I take the recommendation at {SeriesRecords.T.AddMinutes(30).UtcDateTime:HH:mm} UTC");
    }

    // 2026-11-28-d1ffb: off, a recommended question has no deadline and the chat says it waits.
    [Fact]
    public void RecommendedQuestion_DefaultOff_HasNoDeadline_AndSaysItWaits()
    {
        var minutes = SeriesPolicy.QuestionDefaultMinutes(new DialogueGlobalConfig { QuestionDefaultEnabled = false, QuestionDefaultMinutes = 30 });
        var asked = SeriesQuestions.Ask("Keep the original deadline?", ["keep", "restart"], 0, SeriesRecords.T, minutes);

        minutes.Should().BeNull();
        asked.Should().Match<SeriesQuestion>(q => q.DeadlineAt == null && q.Recommended == "keep" && !q.NoDefault);
        SeriesQuestionText.Asked(asked).Should().Contain("Nothing is taken by default (dialogue.question_default_enabled is off): it waits for your answer")
            .And.NotContain("I take the recommendation");
    }

    // 2026-11-28-d1ffb: a question asked with a deadline before defaults were turned off is not taken past it: its deadline is
    // cleared, so no beat is due on it, and the conversation is told once that it waits.
    [Fact]
    public async Task StoredDeadline_DefaultOff_IsNotTaken_AndClearsTheDeadline()
    {
        var off = new DialogueGlobalConfig { QuestionDefaultEnabled = false };
        var record = SeriesRecords.Running() with { Question = Asked(0) };
        await _store.OpenAsync(record, default);
        var beat = new SeriesBeat(record, [], SeriesRecords.T.AddMinutes(121));

        await new SeriesQuestions(_store, AgentSmith.Tests.TestSupport.SeriesWakeDoubles.Budget(_store), off).SettleAsync(beat, default);

        var kept = _store.Records["sr-1"];
        kept.Question!.Should().Match<SeriesQuestion>(q => q.Answer == null && q.DeadlineAt == null && !q.ByDefault);
        SeriesBeatDue.Why(kept, [], beat.Now).Should().NotBe("a question's deadline passed");
        beat.Reasons.Should().BeEmpty("nothing was answered, so nothing reaches the master");
        beat.Said.Should().ContainSingle().Which.Should().Contain("no longer taken by default").And.Contain("Keep the original deadline?");
    }

    // 2026-11-28-b1db: a question carried for a run that only the environment can answer, stored with a deadline before, is
    // never taken by default — it waits without a deadline, and nothing is handed to the master or the run.
    [Fact]
    public async Task CarriedEnvironmentQuestion_StoredWithADeadline_IsNotTakenByDefault()
    {
        var parked = Parked("50", "Inject the disposable database's connection string?", "environment");
        var carried = Asked(0) with { ForTicket = "SCRUM-50", ForRunId = "run-50", ForQuestionId = "q-50" };
        var record = SeriesRecords.Running() with { Raised = [SeriesRunAsk.KeyOf(parked)!] };

        var beat = await BeatAsync(carried, SeriesRecords.T.AddHours(3), [parked], record);

        _store.Records["sr-1"].Question.Should().Match<SeriesQuestion>(q => q.Answer == null && q.NoDefault && q.DeadlineAt == null && !q.ByDefault);
        beat.Said.Should().BeEmpty();
        beat.Reasons.Should().BeEmpty("no answer reaches the master, so the run is told nothing");
        beat.MasterAnswered.Should().BeFalse();
    }

    // A question carried for a ticket whose run waits on no question is no environment question: it defaults as before.
    [Fact]
    public async Task CarriedQuestion_RunParkedOnNothing_IsTakenByDefault()
    {
        var carried = Asked(0) with { ForTicket = "SCRUM-52", ForRunId = "run-52" };

        await BeatAsync(carried, SeriesRecords.T.AddMinutes(121), [SeriesRecords.Fact("52", "running", finished: false)]);

        _store.Records["sr-1"].Question!.ByDefault.Should().BeTrue();
    }

    // 2026-11-28-b1db: a plan question the master carries for a run still takes its recommendation at the deadline.
    [Fact]
    public async Task CarriedPlanQuestion_PastItsDeadline_IsStillTakenByDefault()
    {
        var parked = Parked("52", "Keep the original deadline?", "target");
        var carried = Asked(0) with { ForTicket = "SCRUM-52", ForRunId = "run-52", ForQuestionId = "q-52" };
        var record = SeriesRecords.Running() with { Raised = [SeriesRunAsk.KeyOf(parked)!] };

        var beat = await BeatAsync(carried, SeriesRecords.T.AddMinutes(121), [parked], record);

        _store.Records["sr-1"].Question!.Should().Match<SeriesQuestion>(q => q.Answer == "keep" && q.ByDefault);
        beat.Said.Should().ContainSingle().Which.Should().StartWith("Taken by default at 20:38 UTC: keep");
        beat.Reasons.Should().ContainSingle().Which.Should().Contain("answered by default: keep")
            .And.Contain("tell it only the choice, never that anything was set up");
    }

    [Fact]
    public async Task OnlyAPersonQuestion_HasNoDeadline_AndSaysItWaits()
    {
        var asked = SeriesQuestions.Ask("Grant the pipeline access to the registry?", ["grant", "skip"], 0, SeriesRecords.T, 120, noDefault: true);

        asked.Should().Match<SeriesQuestion>(q => q.DeadlineAt == null && q.NoDefault && q.Recommended == "grant");
        SeriesQuestionText.Asked(asked).Should().Contain("Recommended: grant").And.Contain("nothing is taken by default")
            .And.NotContain("I take the recommendation");
        await BeatAsync(asked, SeriesRecords.T.AddDays(3));
        _store.Records["sr-1"].Question!.Answer.Should().BeNull();
    }

    [Fact]
    public async Task QuestionWithoutRecommendation_WaitsPastAnyTime()
    {
        Asked(null).DeadlineAt.Should().BeNull();

        await BeatAsync(Asked(null), SeriesRecords.T.AddDays(3));

        _store.Records["sr-1"].Question!.Answer.Should().BeNull();
    }

    [Fact]
    public async Task OperatorMessage_AnswersBeforeTheDeadline()
    {
        await BeatAsync(Asked(0), SeriesRecords.T.AddMinutes(5), null, null, "restart it");

        _store.Records["sr-1"].Question!.Should().Match<SeriesQuestion>(q => q.Answer == "restart it" && !q.ByDefault);
    }

    [Fact]
    public async Task ParkedTicketQuestion_IsTheMastersReasonOnce_AndNotRelayed()
    {
        var parked = Parked("52", "Forward keeps the deadline?", "target");

        var beat = await BeatAsync(null, SeriesRecords.T, [parked]);

        beat.Said.Should().BeEmpty("the run asks its master, not the operator");
        _store.Records["sr-1"].Question.Should().BeNull();
        beat.Reasons.Should().ContainSingle().Which.Should().Contain("SCRUM-52's run is parked on a question to you (target; question id q-52)")
            .And.Contain("Forward keeps the deadline?").And.Contain("Choices: keep · restart").And.Contain("It recommends: keep")
            .And.Contain("tell_ticket SCRUM-52 (questionId q-52)").And.Contain("forTicket SCRUM-52");
        beat.Raised.Should().Equal(SeriesRunAsk.KeyOf(parked));
        _store.Records.Clear();
        (await BeatAsync(null, SeriesRecords.T, [parked], SeriesRecords.Running() with { Raised = [SeriesRunAsk.KeyOf(parked)!] }))
            .Reasons.Should().BeEmpty("a question the master judged is not raised again");
    }

    [Fact]
    public async Task EnvironmentQuestion_IsForwardedWithARecommendation_NotFixed()
    {
        var beat = await BeatAsync(null, SeriesRecords.T, [Parked("50", "The registry token is missing.", "environment")]);

        beat.Reasons.Should().ContainSingle().Which.Should().Contain("Only the environment can answer it")
            .And.Contain("naming what to set or grant and where, with your recommendation").And.Contain("It has no default");
    }

    [Fact]
    public async Task SeriesOfOne_ParkedQuestion_IsNoReason_ItsTicketPageAnswersIt()
    {
        // 2026-11-27-ecf2e: only while it has no conversation; with its ticket's, its master leads the question.
        var one = SeriesRecords.Running(conversation: "") with { Tickets = [SeriesRecords.Ticket("52")] };

        var beat = await BeatAsync(null, SeriesRecords.T, [Parked("52", "Which branch?", "target")], one);

        beat.Reasons.Should().BeEmpty();
    }

    // 2026-11-27-ecf2e (operator): a series of one talking in its ticket's conversation leads its run's question there.
    [Fact]
    public async Task SeriesOfOne_WithItsTicketsConversation_ParkedQuestion_IsAReason()
    {
        var one = SeriesRecords.Running() with { Tickets = [SeriesRecords.Ticket("52")] };

        var beat = await BeatAsync(null, SeriesRecords.T, [Parked("52", "Which branch?", "target")], one);

        beat.Reasons.Should().ContainSingle().Which.Should().Contain("Which branch?");
    }

    [Fact]
    public async Task MastersQuestionForATicket_Answered_NamesTheTicketToAnswer()
    {
        var carried = Asked(null) with { ForTicket = "SCRUM-52", ForRunId = "run-52", ForQuestionId = "q-52" };

        var beat = await BeatAsync(carried, SeriesRecords.T.AddMinutes(5), null, null, "15 minutes");

        beat.Reasons.Should().ContainSingle().Which.Should().Contain("(for SCRUM-52's run run-52, question id q-52) is answered: 15 minutes")
            .And.Contain("answer SCRUM-52's run with tell_ticket (questionId q-52)");
    }

    // 2026-11-27-3532b: a master that cannot act (its loop failed, its budget spent) leads no run question: the ticket page answers.
    [Fact]
    public async Task MasterCannotAct_ParkedQuestion_IsNoReason()
    {
        var parked = Parked("52", "Forward keeps the deadline?", "target");

        var failed = await BeatAsync(null, SeriesRecords.T, [parked], SeriesRecords.Running() with { MasterFailedAt = SeriesRecords.T });
        _store.Records.Clear();
        var spent = await BeatAsync(null, SeriesRecords.T, [parked], SeriesRecords.Running() with { MasterCostUsd = 1_000_000m });

        failed.Reasons.Should().BeEmpty();
        spent.Reasons.Should().BeEmpty();
    }

    // 2026-11-27-3532b: the master asks one operator question at a time; when that slot frees, a run question it raised but
    // neither carried nor answered with tell_ticket since is raised again — the carried one is not.
    [Fact]
    public async Task SlotFrees_ARunQuestionNotActedOn_IsRaisedAgain_TheCarriedOneNot()
    {
        var carried = Parked("43", "Which tenant?", "target");
        var waiting = Parked("44", "Which branch?", "target");
        var asked = Asked(null) with { ForTicket = "SCRUM-43", ForRunId = "run-43", ForQuestionId = "q-43" };
        var record = SeriesRecords.Running() with { Raised = [SeriesRunAsk.KeyOf(carried)!, SeriesRunAsk.KeyOf(waiting)!] };

        var beat = await BeatAsync(asked, SeriesRecords.T.AddMinutes(5), [carried, waiting], record, "tenant one");

        beat.Reasons.Should().HaveCount(2);
        beat.Reasons.Should().Contain(r => r.Contains("(for SCRUM-43's run run-43, question id q-43) is answered: tenant one"));
        beat.Reasons.Should().Contain(r => r.Contains("SCRUM-44's run is parked") && r.Contains("Which branch?"));
        _store.Records["sr-1"].Raised.Should().Contain(SeriesRunAsk.KeyOf(carried)!);
    }

    [Fact]
    public async Task SlotFrees_ARunQuestionToldSince_IsNotRaisedAgain()
    {
        var waiting = Parked("44", "Which branch?", "target");
        var record = SeriesRecords.Running() with
        {
            Raised = [SeriesRunAsk.KeyOf(waiting)!],
            Acts = [new SeriesAct(SeriesRecords.T.AddMinutes(1), "tell_ticket", "44", "wrote to SCRUM-44", null)],
        };

        var beat = await BeatAsync(Asked(null), SeriesRecords.T.AddMinutes(5), [waiting], record, "go on");

        beat.Reasons.Should().ContainSingle().Which.Should().Contain("is answered: go on");
    }

    // 2026-11-27-3532b: the answer counts as delivered only once the master judged it — a failed loop is handed it again.
    [Fact]
    public async Task AnsweredQuestion_NotYetJudged_IsHandedAgain()
    {
        await BeatAsync(Asked(null), SeriesRecords.T.AddMinutes(5), null, null, "restart it");
        var kept = _store.Records["sr-1"];
        kept.Question!.Delivered.Should().BeFalse();

        var again = new SeriesBeat(kept, [], SeriesRecords.T.AddMinutes(9));
        await Questions.SettleAsync(again, default);

        again.Reasons.Should().ContainSingle().Which.Should().Contain("is answered: restart it");
        again.MasterAnswered.Should().BeTrue();
    }

    // 2026-11-27-3532e: the question is a plain chat message with one short choices line — where no quick replies are drawn
    // (a closed conversation, Work it out) the choices still read.
    [Fact]
    public void Asked_IsThePlainQuestion_WithOneChoicesLineAndItsRecommendation()
    {
        var text = SeriesQuestionText.Asked(Asked(0));

        text.Should().Contain("**Keep the original deadline?**").And.Contain("\n\nChoices: keep / restart").And.Contain("Recommended: keep");
        SeriesQuestionText.Asked(SeriesQuestions.Ask("Go on?", [], null, SeriesRecords.T, 120)).Should().NotContain("Choices:");
    }

    [Fact]
    public void TicketMasterQuestion_RecommendedIndex_ReachesTheCheckpointQuestion()
    {
        var composed = MasterQuestionCheckpoint.Compose(
            [new PlanOpenQuestion("1", "Keep it?", ["keep", "drop"]) { RecommendedIndex = 1 }], "ask-1");

        composed.RecommendedIndex.Should().Be(1);
        composed.DefaultAnswer.Should().BeNull("outside a series nothing changes: it still waits for a person");
    }
}
