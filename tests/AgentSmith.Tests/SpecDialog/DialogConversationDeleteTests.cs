using System.Security.Claims;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-18-7a05: an operator deletes a design conversation they own, over the REAL durable
/// store (SQLite in-memory with the shipped migrations).
/// <para>
/// The load-bearing cases are the ones the surface's EXISTING owner check would have let
/// through. That check is keyed on the THREAD id and passes on a miss, and a dialog id is
/// client-supplied, so a caller passing another principal's SESSION id meets a lookup that
/// either misses — and passes — or finds the conversation they opened under it themselves.
/// Both branches are asserted here, against a check keyed on the session id in which an absent
/// row is a refusal.
/// </para>
/// </summary>
public sealed class DialogConversationDeleteTests : IDisposable
{
    private const string Platform = "dashboard";
    private const string Owner = "person-a";
    private const string Intruder = "person-b";

    private readonly SqliteConnection _connection;
    private readonly AgentSmithDbContext _context;
    private readonly SpecDialogSessionRepository _repository;
    private readonly DialogueAnswerRepository _answers;
    private readonly SpecDialogSessionManager _sessions;
    private readonly SpecDialogOwnership _ownership;
    private readonly SpecDialogTurnGate _gate = new(TimeProvider.System);

    public DialogConversationDeleteTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _context = new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        _context.Database.Migrate();
        _repository = new SpecDialogSessionRepository(_context);
        _answers = new DialogueAnswerRepository(_context, new SqliteUniqueViolationTranslator());
        _sessions = new SpecDialogSessionManager(
            _repository, AgentSmith.Tests.Sandbox.Holds.None(), TimeProvider.System,
            NullLogger<SpecDialogSessionManager>.Instance);
        _ownership = new SpecDialogOwnership(_repository);
    }

    [Fact]
    public async Task Delete_OwnConversation_RemovesTheRow()
    {
        var session = await OpenAsync("d-1");

        var result = await DeleteAsync(session, Owner);

        StatusOf(result).Should().Be(StatusCodes.Status204NoContent);
        (await SessionIdsAsync()).Should().BeEmpty("a deleted conversation is gone, not closed");
    }

    [Fact]
    public async Task Delete_OwnConversation_SweepsItsDurableAnswers()
    {
        var session = await OpenAsync("d-1");
        await AnswerAsync(session, "q-1", "approve");
        await AnswerAsync("2026-09-18T09-00-00-0001", "q-1", "an unrelated run's answer");

        await DeleteAsync(session, Owner);

        (await AnswerJobsAsync()).Should().BeEquivalentTo(["2026-09-18T09-00-00-0001"],
            "the operator's own answers go with the conversation, and nothing else does");
    }

    [Fact]
    public async Task Delete_AnotherPrincipalsConversation_LeavesItInPlace()
    {
        var session = await OpenAsync("d-1");

        var result = await DeleteAsync(session, Intruder);

        StatusOf(result).Should().Be(StatusCodes.Status204NoContent);
        (await SessionIdsAsync()).Should().Equal(session);
    }

    /// <summary>
    /// The bypass, written out. The attacker opens a conversation of their OWN passing the
    /// victim's session id as the dialog id — the id is checked only for emptiness and becomes
    /// the thread id verbatim — so the thread-keyed lookup finds a row they own and its owner
    /// comparison succeeds. A check keyed on the session id finds the victim's row instead.
    /// </summary>
    [Fact]
    public async Task Delete_ASessionIdOpenedAsAnotherPrincipalsThreadId_IsStillRefused()
    {
        var session = await OpenAsync("d-1");
        var theirs = await OpenAsync(session, Intruder);
        (await _ownership.MayWatchAsync(session, Intruder, CancellationToken.None))
            .Should().BeTrue("this is the check that must not decide a delete");

        var result = await DeleteAsync(session, Intruder);

        StatusOf(result).Should().Be(StatusCodes.Status204NoContent);
        (await SessionIdsAsync()).Should().BeEquivalentTo([session, theirs]);
    }

    [Fact]
    public async Task Delete_SessionThatDoesNotExist_AnswersAsForOneThatIsNotYours()
    {
        var session = await OpenAsync("d-1");

        var missing = await DeleteAsync("s-never-existed", Owner);
        var foreign = await DeleteAsync(session, Intruder);

        StatusOf(missing).Should().Be(StatusOf(foreign),
            "an id that exists and an id that does not must be indistinguishable here");
        StatusOf(missing).Should().Be(StatusCodes.Status204NoContent);
    }

    [Fact]
    public async Task Delete_TwiceInARow_AnswersTheSameBothTimes()
    {
        var session = await OpenAsync("d-1");

        var first = await DeleteAsync(session, Owner);
        var second = await DeleteAsync(session, Owner);

        StatusOf(second).Should().Be(StatusOf(first), "a second click is not an error");
        StatusOf(first).Should().NotBe(StatusCodes.Status403Forbidden,
            "403 is what the authorization layer answers for a missing permission — a route "
            + "answering it itself would pass its permission test with the requirement deleted");
    }

    [Fact]
    public async Task Delete_AConversationOnAnotherPlatform_IsNotReached()
    {
        var elsewhere = (await _sessions.OpenAsync(
            "slack", "C-1", "C-1", Owner, Scope(), CancellationToken.None)).JobId;

        var result = await DeleteAsync(elsewhere, Owner);

        StatusOf(result).Should().Be(StatusCodes.Status204NoContent);
        (await SessionIdsAsync()).Should().BeEquivalentTo([elsewhere],
            "the platform scoping stops this route reaching a conversation on another surface");
    }

    /// <summary>
    /// The span is the gate's HOLD, not the computing flag. The router calls the outcome flow —
    /// the approval wait and the filing — after the turn runner returns and inside the hold, so
    /// a delete admitted whenever nothing is computing would land in the middle of a filing.
    /// </summary>
    [Fact]
    public async Task Delete_WhileTheHoldIsTaken_IsRefusedWithThatReason_EvenWhenTheTurnIsNotComputing()
    {
        var session = await OpenAsync("d-1");
        _gate.TryEnter(session).Should().BeTrue();
        _gate.Liveness(session).Computing.Should().BeFalse("nothing is computing — the hold is what refuses");

        var result = await DeleteAsync(session, Owner);

        StatusOf(result).Should().Be(StatusCodes.Status409Conflict);
        (await SessionIdsAsync()).Should().Equal(session);
    }

    [Fact]
    public async Task Delete_ThatFailsAfterTakingTheHold_LeavesTheConversationUsable()
    {
        var session = await OpenAsync("d-1");
        var failing = new Mock<ISpecDialogConversationDeleter>();
        failing.Setup(d => d.DeleteAsync(session, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("the transaction could not commit"));

        var thrown = async () => await SpecDialogDeletionEndpoints.DeleteAsync(
            session, Principal(Owner), _ownership, _gate, failing.Object,
            AgentSmith.Tests.Sandbox.Holds.None(), CancellationToken.None);

        await thrown.Should().ThrowAsync<InvalidOperationException>();
        _gate.TryEnter(session).Should().BeTrue(
            "the hold is released in a finally, so a failed delete does not wedge the conversation");
    }

    /// <summary>
    /// The owner check runs BEFORE the acquire, and this is why that ordering is a step rather
    /// than a remark. Mis-ordered, a non-owner would take the gate on a session they do not own:
    /// a loop of refused deletes would bounce every one of that owner's turns off it.
    /// </summary>
    [Fact]
    public async Task Delete_AnotherPrincipalsHeldConversation_AnswersAsForOneThatIsNotYours()
    {
        var session = await OpenAsync("d-1");
        _gate.TryEnter(session).Should().BeTrue();

        var result = await DeleteAsync(session, Intruder);

        StatusOf(result).Should().Be(StatusCodes.Status204NoContent,
            "the held answer is this route's only asymmetry, so a non-owner must never see it");
        _gate.TryEnter(session).Should().BeFalse(
            "the refused delete neither took the owner's hold nor exited it");
    }

    /// <summary>
    /// The approved set STAYS. Every work ticket the dialog files carries the approved stamp,
    /// and a stamped ticket with no stored set is refused at the spec gate on every trigger — so
    /// sweeping it would leave the filed ticket permanently unworkable, with no route back.
    /// The tickets stay because nothing here can reach a tracker: the delete's whole dependency
    /// graph is resolved from a container that holds one, and it is never asked for.
    /// </summary>
    [Fact]
    public async Task Delete_ConversationThatFiled_LeavesTheTicketsAndTheApprovedSetStored()
    {
        var session = await OpenAsync("d-1");
        var approved = new ApprovedSpecSetRepository(_context);
        await approved.SaveAsync(
            ApprovedSets.Record("jira-19106", ApprovedSets.Noon, conversation: session),
            CancellationToken.None);
        var tracker = new Mock<ITicketProvider>(MockBehavior.Strict);
        var trackers = new Mock<ITicketProviderFactory>(MockBehavior.Strict);

        await DeleteAsync(session, Owner, Resolved(tracker, trackers));

        (await SessionIdsAsync()).Should().BeEmpty();
        (await approved.GetAsync(ApprovedSets.Tracker, "jira-19106", CancellationToken.None))
            .Should().NotBeNull("the approved set outlives the conversation that ratified it");
        tracker.Invocations.Should().BeEmpty("a filing is not undone by forgetting the conversation");
        trackers.Invocations.Should().BeEmpty();
    }

    /// <summary>The delete's real graph, built by the real container rather than by hand.</summary>
    private ISpecDialogConversationDeleter Resolved(
        Mock<ITicketProvider> tracker, Mock<ITicketProviderFactory> trackers) =>
        new ServiceCollection()
            .AddSingleton<IUnitOfWork>(_context)
            .AddSingleton(new SqliteUniqueViolationTranslator())
            .AddSingleton<IUniqueViolationTranslator>(sp =>
                sp.GetRequiredService<SqliteUniqueViolationTranslator>())
            .AddScoped<SpecDialogSessionRepository>()
            .AddScoped<DialogueAnswerRepository>()
            .AddScoped<SpecDialogAttachmentRepository>()
            .AddSingleton(tracker.Object)
            .AddSingleton(trackers.Object)
            .AddScoped<ISpecDialogConversationDeleter, SpecDialogConversationDeleter>()
            .BuildServiceProvider()
            .GetRequiredService<ISpecDialogConversationDeleter>();

    private Task<IResult> DeleteAsync(
        string sessionId, string caller, ISpecDialogConversationDeleter? deleter = null) =>
        SpecDialogDeletionEndpoints.DeleteAsync(
            sessionId, Principal(caller), _ownership, _gate,
            deleter ?? new SpecDialogConversationDeleter(
                _context, _repository, _answers, new SpecDialogAttachmentRepository(_context)),
            AgentSmith.Tests.Sandbox.Holds.None(), CancellationToken.None);

    private static int StatusOf(IResult result) =>
        result.Should().BeAssignableTo<IStatusCodeHttpResult>().Which.StatusCode
        ?? throw new InvalidOperationException("the route answered without a status code");

    private async Task<string> OpenAsync(string dialogId, string owner = Owner) =>
        (await _sessions.OpenAsync(
            Platform, dialogId, dialogId, owner, Scope(), CancellationToken.None)).JobId;

    private Task AnswerAsync(string job, string questionId, string answer) =>
        _answers.TryDeliverAsync(
            job, new DialogAnswer(questionId, answer, null, DateTimeOffset.UtcNow, Owner),
            CancellationToken.None);

    private async Task<IReadOnlyList<string>> SessionIdsAsync()
    {
        _context.ChangeTracker.Clear(); // a bulk delete is not tracked; read what the store holds
        return await _context.Set<SpecDialogSession>()
            .AsNoTracking().Select(s => s.SessionId).ToListAsync();
    }

    private async Task<IReadOnlyList<string>> AnswerJobsAsync()
    {
        _context.ChangeTracker.Clear();
        return await _context.Set<DialogueAnswerEntry>()
            .AsNoTracking().Select(a => a.DialogueJobId).ToListAsync();
    }

    private static ActiveScope Scope() => new() { Project = "sample", Repos = ["repo-a"] };

    private static ClaimsPrincipal Principal(string subject) =>
        new(new ClaimsIdentity([new Claim("sub", subject)], "test"));

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
