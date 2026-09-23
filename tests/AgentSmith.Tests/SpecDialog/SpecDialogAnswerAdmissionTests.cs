using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-17-042el: a thread message answering a pending question is stored with what it
/// decided, before the question is taken and the answer published — over the real store.
/// </summary>
public sealed class SpecDialogAnswerAdmissionTests : IDisposable
{
    private const string Platform = "dashboard";
    private const string Thread = "d-042el";

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AgentSmithDbContext _context;
    private readonly SpecDialogSessionManager _sessions;
    private readonly SwitchableClock _clock = new();
    private readonly SpecDialogPendingQuestions _pending = new(new SpecDialogTurnGate(TimeProvider.System));
    private readonly Mock<IDialogueTransport> _transport = new();
    private readonly SpecDialogAnswerAdmission _admission;

    public SpecDialogAnswerAdmissionTests()
    {
        _connection.Open();
        _context = new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        _context.Database.Migrate();
        _sessions = new SpecDialogSessionManager(
            new SpecDialogSessionRepository(_context), AgentSmith.Tests.Sandbox.Holds.None(), _clock,
            NullLogger<SpecDialogSessionManager>.Instance);
        _admission = new SpecDialogAnswerAdmission(_sessions, _pending, _transport.Object);
    }

    [Fact]
    public async Task Admission_ApprovalAnswer_IsAppendedWithItsDecision()
    {
        var sessionId = await OpenWithAsync(QuestionType.Approval);

        var admitted = await AdmitAsync(" Approve ");

        admitted!.Answered.Should().BeTrue();
        admitted.State.Transcript.Should().ContainSingle()
            .Which.Decision.Should().Be(SpecDialogDecision.Approved);
        _transport.Verify(t => t.PublishAnswerAsync(sessionId,
            It.Is<DialogAnswer>(a => a.QuestionId == "q-1" && a.Answer == " Approve "),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Admission_EditNoteToAConfirmation_IsAnOrdinaryMessage()
    {
        await OpenWithAsync(QuestionType.Approval);

        var admitted = await AdmitAsync("split it into two slices");

        admitted!.Answered.Should().BeTrue("an edit note is still the answer");
        admitted.State.Transcript.Should().ContainSingle().Which.Decision.Should().BeNull();
    }

    [Fact]
    public async Task Admission_FreeTextAskHumanAnswer_IsAppendedOnce()
    {
        await OpenWithAsync(QuestionType.FreeText);

        var admitted = await AdmitAsync("yes");

        admitted!.Answered.Should().BeTrue();
        admitted.State.Transcript.Should().ContainSingle().Which.Decision.Should().BeNull();
        (await AdmitAsync("and one more thing"))!.Answered.Should().BeFalse("the question was taken");
    }

    [Fact]
    public async Task Admission_EditNote_IsAppendedBeforeTheAnswerIsPublished()
    {
        await OpenWithAsync(QuestionType.Approval);
        var heldWhenPublished = new List<string>();
        _transport.Setup(t => t.PublishAnswerAsync(
                It.IsAny<string>(), It.IsAny<DialogAnswer>(), It.IsAny<CancellationToken>()))
            .Returns(async () => heldWhenPublished.AddRange(
                (await _sessions.GetOpenByThreadAsync(Platform, Thread, CancellationToken.None))!
                .Transcript.Select(turn => turn.Text)));

        await AdmitAsync("cut it smaller");

        heldWhenPublished.Should().Equal("cut it smaller");
    }

    [Fact]
    public async Task Admission_AppendFails_QuestionStaysAnswerable()
    {
        await OpenWithAsync(QuestionType.Approval);
        _clock.Fail = true;

        var failed = async () => await AdmitAsync("approve");

        await failed.Should().ThrowAsync<InvalidOperationException>();
        _pending.TryPeek(Session(), out _).Should().BeTrue("the question was not taken");
        _clock.Fail = false;
        (await AdmitAsync("approve"))!.Answered.Should().BeTrue();
    }

    [Fact]
    public async Task Admission_QuestionEndsDuringTheAppend_StoresNoDecision()
    {
        var sessionId = await OpenWithAsync(QuestionType.Approval);
        _clock.OnNow = () => _pending.Clear(sessionId);

        var admitted = await AdmitAsync("approve");

        admitted!.Answered.Should().BeFalse("the question it was stored against is gone");
        (await StoredAsync()).Should().ContainSingle().Which.Decision.Should().BeNull();
        admitted.State.Transcript.Should().ContainSingle().Which.Decision.Should().BeNull();
        _transport.Verify(t => t.PublishAnswerAsync(
            It.IsAny<string>(), It.IsAny<DialogAnswer>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Admission_TwoAnswersToOneQuestion_OnlyTheOneTakenKeepsItsDecision()
    {
        await OpenWithAsync(QuestionType.Approval);
        SpecDialogAdmitted? second = null;
        _clock.OnNow = () => second = AdmitAsync("approve").GetAwaiter().GetResult();

        var first = await AdmitAsync("approve");

        second!.Answered.Should().BeTrue();
        first!.Answered.Should().BeFalse("the other message took the question");
        (await StoredAsync()).Select(turn => turn.Decision)
            .Should().Equal(SpecDialogDecision.Approved, null);
        _transport.Verify(t => t.PublishAnswerAsync(
            It.IsAny<string>(), It.IsAny<DialogAnswer>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Admission_QuestionReplacedDuringTheAppend_IsNotTaken()
    {
        var sessionId = await OpenWithAsync(QuestionType.Approval);
        var replacement = Question(QuestionType.FreeText) with { QuestionId = "q-2" };
        _clock.OnNow = () => _pending.Set(sessionId, replacement, expiresAt: null);

        var admitted = await AdmitAsync("approve");

        admitted!.Answered.Should().BeFalse("it was stored against the question that was replaced");
        (await StoredAsync()).Should().ContainSingle().Which.Decision.Should().BeNull();
        _pending.TryPeek(sessionId, out var still).Should().BeTrue();
        still.Question.QuestionId.Should().Be("q-2");
    }

    [Theory]
    [InlineData(QuestionType.FreeText, "yes", null)]
    [InlineData(QuestionType.Approval, "YES", SpecDialogDecision.Approved)]
    [InlineData(QuestionType.Approval, " drop ", SpecDialogDecision.Rejected)]
    [InlineData(QuestionType.Approval, "yes, but smaller", null)]
    public void Classifier_YesToAFreeTextQuestion_IsNotADecision(
        QuestionType type, string reply, SpecDialogDecision? expected) =>
        SpecDialogAnswerWords.DecisionOn(Question(type), reply).Should().Be(expected);

    [Fact]
    public void TranscriptTurn_RowWithoutDecision_ReadsAsAMessage()
    {
        var turns = SpecDialogSessionMapper.ReadTranscript(
            """[{"role":"user","text":"approve","at":"2026-09-01T00:00:00+00:00"}]""");
        var written = SpecDialogSessionMapper.WriteTranscript(
            [turns[0] with { Decision = SpecDialogDecision.Rejected }]);

        turns.Should().ContainSingle().Which.Decision.Should().BeNull();
        SpecDialogShownTranscript.Turns(turns).Single().Decision.Should().BeNull();
        var reread = SpecDialogSessionMapper.ReadTranscript(written);
        SpecDialogShownTranscript.Turns(reread).Single().Decision.Should().Be("rejected");
    }

    [Theory]
    [InlineData("\"escalated\"")]
    [InlineData("0")]
    [InlineData("7")]
    [InlineData("\"approved, rejected\"")]
    [InlineData("{\"word\":\"approved\"}")]
    public void TranscriptTurn_DecisionThisBuildCannotName_ReadsAsNone(string stored)
    {
        var turns = SpecDialogSessionMapper.ReadTranscript(
            $$"""[{"role":"user","text":"approve","at":"2026-09-01T00:00:00+00:00","decision":{{stored}}},"""
            + """{"role":"user","text":"reject","at":"2026-09-01T00:01:00+00:00","decision":"rejected"}]""");

        turns.Select(turn => turn.Decision).Should().Equal(null, SpecDialogDecision.Rejected);
    }

    private async Task<string> OpenWithAsync(QuestionType type)
    {
        var state = await _sessions.OpenAsync(Platform, Thread, Thread, "U1",
            new ActiveScope { Project = "sample", Repos = ["repo-a"] }, CancellationToken.None);
        _pending.Set(state.JobId, Question(type), expiresAt: null);
        return state.JobId;
    }

    private async Task<IReadOnlyList<TranscriptTurn>> StoredAsync() =>
        (await _sessions.GetOpenByThreadAsync(Platform, Thread, CancellationToken.None))!.Transcript;

    private string Session() => _sessions.GetOpenByThreadAsync(Platform, Thread, CancellationToken.None)
        .GetAwaiter().GetResult()!.JobId;

    private Task<SpecDialogAdmitted?> AdmitAsync(string text) =>
        _admission.AdmitAsync(text, "U1", Platform, Thread, CancellationToken.None);

    private static DialogQuestion Question(QuestionType type) =>
        new("q-1", type, "file it?", Context: null, Choices: null, DefaultAnswer: "", TimeSpan.FromMinutes(15));

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private sealed class SwitchableClock : TimeProvider
    {
        public bool Fail { get; set; }

        /// <summary>Runs once, inside the append — between the peek and the take.</summary>
        public Action? OnNow { get; set; }

        public override DateTimeOffset GetUtcNow()
        {
            if (Fail) throw new InvalidOperationException("the store is unavailable");
            var hook = OnNow;
            OnNow = null;
            hook?.Invoke();
            return base.GetUtcNow();
        }
    }
}
