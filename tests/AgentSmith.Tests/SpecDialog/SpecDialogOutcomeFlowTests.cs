using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-11-12-92e9c: on the dashboard a shown turn never changes, so a newer proposal posts no notice declaring the
/// one above void — on the design path or the steward's. A chat thread keeps the notice: its card stays postable.
/// </summary>
public sealed class SpecDialogOutcomeFlowTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AgentSmithDbContext _context;

    public SpecDialogOutcomeFlowTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _context = new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        MigratedStoreTemplate.CopyInto(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Superseded_NotPostedOnDashboard_PostedOnChat()
    {
        (await SupersededNoticesAsync("dashboard", "d-92e9c")).Should().Be(0,
            "the dashboard's history never declares a shown turn void");
        (await SupersededNoticesAsync("slack", "th-92e9c")).Should().Be(2,
            "a chat thread is told on the design path and on the steward's");
    }

    [Fact]
    public async Task HandleAsync_WithdrawnAnswer_RejectsTheAwaitingProposal()
    {
        // 2026-11-19-7c42a: the operator said to discard the draft and the turn deleted it.
        var harness = new SpecDialogDecisionHarness(_context, sink: Mock.Of<IOutcomeSink>());
        var state = await harness.OpenAsync("dashboard", "d-7c42a");
        await harness.Flow().HandleAsync(state, Spec("p1"), default);

        await harness.Flow().HandleAsync(state, new AnswerOutcome { Withdrawn = true }, default);

        (await harness.Proposals.ReadAsync(state, default)).AwaitingId.Should().BeNull("a discarded draft awaits nothing");
    }

    [Fact]
    public async Task HandleAsync_PlainAnswer_LeavesTheAwaitingProposal()
    {
        var harness = new SpecDialogDecisionHarness(_context, sink: Mock.Of<IOutcomeSink>());
        var state = await harness.OpenAsync("dashboard", "d-7c42a2");
        await harness.Flow().HandleAsync(state, Spec("p1"), default);

        await harness.Flow().HandleAsync(state, new AnswerOutcome(), default);

        (await harness.Proposals.ReadAsync(state, default)).AwaitingId.Should().NotBeNull("an answer that changed nothing decides nothing");
    }

    private async Task<int> SupersededNoticesAsync(string platform, string thread)
    {
        var harness = new SpecDialogDecisionHarness(_context, sink: Mock.Of<IOutcomeSink>());
        var state = await harness.OpenAsync(platform, thread);
        await harness.Flow().HandleAsync(state, Spec("p1"), default);
        await harness.Flow().HandleAsync(state, Spec("p2"), default);
        await harness.Flow().HandleAsync(state, new AnswerOutcome(), default, stewarded: true);
        var transcript = (await harness.Sessions.GetOpenByThreadAsync(platform, thread, default))!.Transcript;
        return transcript.Count(t => t.Kind == SpecDialogTurnKind.Notice && t.Text.Contains("replaced by the one below"));
    }

    private static SpecOutcome Spec(string id) => new(new SpecDraft(id, $"goal of {id}", $"spec: {id}", []));
}
