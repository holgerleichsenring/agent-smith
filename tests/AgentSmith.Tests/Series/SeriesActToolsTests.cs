using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-17-0207a: the series master's acts reach the ticket's own paths as the approver, refuse an unknown
/// ticket and an approver without runs.control, and stop ends the series.</summary>
public sealed class SeriesActToolsTests
{
    private readonly InMemorySeriesRunStore _store = new();
    private readonly Mock<ISeriesTicketActs> _acts = new();
    private readonly Mock<ISeriesTicketFacts> _facts = new();
    private readonly Mock<ISeriesConversation> _conversation = new();

    private SeriesActTools Tools(SeriesRunRecord record) =>
        new(new SeriesBeat(record, [.. record.Tickets.Select(t => new SeriesTicketFact(t))], SeriesRecords.T), _acts.Object, _store,
            new SeriesPause(_store, _facts.Object, _acts.Object, _conversation.Object));

    // SCRUM-43 is coding, SCRUM-44 waits on it, SCRUM-45 never started and waits on nothing.
    private void Facts(bool landed43 = false) =>
        _facts.Setup(f => f.ReadAsync("sample", It.IsAny<IReadOnlyList<SeriesTicket>>(), It.IsAny<CancellationToken>())).ReturnsAsync(
            [SeriesRecords.Fact("43", landed43 ? RunStatuses.Success : RunStatuses.Running, finished: landed43,
                landed: landed43 ? SeriesRecords.T : null),
             SeriesRecords.Fact("44", requires: "43"), SeriesRecords.Fact("45")]);

    [Fact]
    public async Task RetryTicket_KnownTicket_ReachesThePortAsTheApprover()
    {
        _acts.Setup(a => a.RetryAsync("sample", "44", "operator", It.IsAny<CancellationToken>())).ReturnsAsync("retried");

        (await Tools(SeriesRecords.Running()).RetryTicketAsync("SCRUM-44")).Should().Be("retried");
    }

    [Fact]
    public async Task TellTicket_ApproverWithoutRunsControl_ActsOnNoTicket()
    {
        var said = await Tools(SeriesRecords.Running(mayStartRuns: false)).TellTicketAsync("SCRUM-44", "use German");

        said.Should().Contain("runs.control");
        _acts.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task StartTicket_UnknownTicket_NamesTheSeriesTickets()
    {
        (await Tools(SeriesRecords.Running()).StartTicketAsync("SCRUM-99")).Should().Contain("SCRUM-43, SCRUM-44, SCRUM-45");
    }

    [Fact]
    public async Task StopSeries_EndsTheRecordAndSaysWhy()
    {
        var record = SeriesRecords.Running();
        await _store.OpenAsync(record, default);
        var tools = Tools(record);

        await tools.StopSeriesAsync("the operator asked");

        _store.Records["sr-1"].State.Should().Be(SeriesRunStates.Stopped);
    }

    private void Starts(string ticket, bool started, string text) =>
        _acts.Setup(a => a.StartIfReadyAsync("sample", ticket, It.IsAny<CancellationToken>())).ReturnsAsync(new SeriesStart(started, text));

    [Fact]
    public async Task SeriesActTools_PauseAndResume_ChangeTheState()
    {
        var record = SeriesRecords.Running();
        await _store.OpenAsync(record, default);
        Facts();
        Starts("44", false, "This ticket waits for #43 to be merged first; merge them, or mark them done by hand.");
        Starts("45", true, "Ticket 45: Started — moved");
        var tools = Tools(record);

        var paused = await tools.PauseSeriesAsync();

        _store.Records["sr-1"].State.Should().Be(SeriesRunStates.Paused);
        paused.Should().Be($"The series is paused: {SeriesPauseText.Holds} SCRUM-43 finishes the run under way. "
            + "Say resume, or press Resume, to go on.");
        _acts.Verify(a => a.CancelAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        (await tools.StartTicketAsync("SCRUM-45")).Should().Contain("paused");

        var resumed = await tools.ResumeSeriesAsync();

        _store.Records["sr-1"].State.Should().Be(SeriesRunStates.Running);
        _store.Records["sr-1"].PausedFrom.Should().BeNull();
        resumed.Should().StartWith("The series resumes and starts SCRUM-45.", "a start the requirements refused is not claimed");
        _conversation.Verify(c => c.SayAsync("conv-1", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // Under on_green_push a ticket starts on its prerequisite's green branch before that one lands; the resume offers it
    // to the start, which reads requirements as every start does, instead of judging landings itself.
    [Fact]
    public async Task ResumeSeries_GreenPushPrerequisite_StartsTheDependentTheStartAdmits()
    {
        var record = SeriesRecords.Running() with { State = SeriesRunStates.Paused, PausedFrom = SeriesRunStates.Running };
        await _store.OpenAsync(record, default);
        Facts();
        Starts("44", true, "Ticket 44: Started — stacked on #43");
        Starts("45", false, "Ticket 45 waits for a start by name.");

        (await Tools(record).ResumeSeriesAsync()).Should().StartWith("The series resumes and starts SCRUM-44.");
    }

    [Fact]
    public async Task ResumeSeries_RedTicket_IsNamedForARetry()
    {
        var record = SeriesRecords.Running() with { State = SeriesRunStates.Paused, PausedFrom = SeriesRunStates.Running };
        await _store.OpenAsync(record, default);
        _facts.Setup(f => f.ReadAsync("sample", It.IsAny<IReadOnlyList<SeriesTicket>>(), It.IsAny<CancellationToken>())).ReturnsAsync(
            [SeriesRecords.Fact("43", RunStatuses.Failed), SeriesRecords.Fact("44", RunStatuses.Success), SeriesRecords.Fact("45", RunStatuses.Success)]);

        (await Tools(record).ResumeSeriesAsync()).Should()
            .Be("The series resumes. SCRUM-43 is red and waits for a retry; say retry to run it again.");
    }

    [Fact]
    public async Task PauseSeries_AlreadyPaused_SaysNothingAgain()
    {
        var record = SeriesRecords.Running() with { State = SeriesRunStates.Paused, PausedFrom = SeriesRunStates.Running };
        await _store.OpenAsync(record, default);

        (await Tools(record).PauseSeriesAsync()).Should().Be("The series is already paused.");
        _conversation.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PauseSeries_StopNow_CancelsTheRunUnderWay()
    {
        var record = SeriesRecords.Running();
        await _store.OpenAsync(record, default);
        Facts();

        var said = await Tools(record).PauseSeriesAsync(stopNow: true);

        said.Should().Contain("The run of SCRUM-43 is cancelled.");
        _acts.Verify(a => a.CancelAsync("run-43", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PauseSeries_Verifying_ResumesToVerifying()
    {
        var record = SeriesRecords.Running() with { State = SeriesRunStates.Verifying };
        await _store.OpenAsync(record, default);
        Facts(landed43: true);
        Starts("44", false, "not yet");
        Starts("45", false, "not yet");
        var tools = Tools(record);

        await tools.PauseSeriesAsync();
        await tools.ResumeSeriesAsync();

        _store.Records["sr-1"].State.Should().Be(SeriesRunStates.Verifying);
    }

    [Fact]
    public async Task ResumeSeries_NotPaused_ChangesNothing()
    {
        var record = SeriesRecords.Running();
        await _store.OpenAsync(record, default);

        (await Tools(record).ResumeSeriesAsync()).Should().Be("The series is running, not paused.");
        _conversation.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PauseSeries_QueuedAndParkedTickets_AreNotSaidToFinish()
    {
        var record = SeriesRecords.Running();
        await _store.OpenAsync(record, default);
        _facts.Setup(f => f.ReadAsync("sample", It.IsAny<IReadOnlyList<SeriesTicket>>(), It.IsAny<CancellationToken>())).ReturnsAsync(
            [SeriesRecords.Fact("43", RunStatuses.Queued, finished: false), SeriesRecords.Fact("44", RunStatuses.WaitingForInput, finished: false),
             SeriesRecords.Fact("45")]);

        var said = await Tools(record).PauseSeriesAsync(stopNow: true);

        said.Should().Be($"The series is paused: {SeriesPauseText.Holds} SCRUM-43's queued start is held. SCRUM-44 waits for your answer. "
            + "Say resume, or press Resume, to go on.");
        _acts.Verify(a => a.CancelAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never, "nothing was running");
    }

    [Fact]
    public async Task ResumeSeries_RedCi_GoesBackToTheCiLoop_AndAQueuedTicketIsOfferedToTheStart()
    {
        var record = SeriesRecords.Running() with { State = SeriesRunStates.Paused, PausedFrom = SeriesRunStates.Running };
        await _store.OpenAsync(record, default);
        _facts.Setup(f => f.ReadAsync("sample", It.IsAny<IReadOnlyList<SeriesTicket>>(), It.IsAny<CancellationToken>())).ReturnsAsync(
            [SeriesRecords.Fact("43", RunStatuses.Success) with { CiState = AgentSmith.Contracts.Reviews.CiStates.Red },
             SeriesRecords.Fact("44", RunStatuses.Queued, finished: false), SeriesRecords.Fact("45", RunStatuses.Success)]);
        Starts("44", true, "Ticket 44 started: moved");
        _acts.Setup(a => a.AskCiLoopAsync("sample", "43", It.IsAny<CancellationToken>())).ReturnsAsync("back to the loop");

        (await Tools(record).ResumeSeriesAsync()).Should()
            .StartWith("The series resumes and starts SCRUM-44. The red CI of SCRUM-43 goes back to the CI loop.");
        _acts.Verify(a => a.AskCiLoopAsync("sample", "43", It.IsAny<CancellationToken>()), Times.Once);
    }
}
