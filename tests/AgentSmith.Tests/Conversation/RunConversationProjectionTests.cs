using System.Text.Json;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RunEvent = AgentSmith.Contracts.Events.RunEvent;

namespace AgentSmith.Tests.Conversation;

/// <summary>
/// 2026-10-13-9a59g: a ticket run's conversation through the composition root's own registration, over a
/// migrated SQLite store — one row per turn and trail position, every question kept although the checkpoint
/// keeps only the latest, nothing for a ticketless park, and the rows living exactly as long as the run.
/// </summary>
public sealed class RunConversationProjectionTests : IDisposable
{
    private static readonly DateTimeOffset T = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);
    private const string RunId = "2026-10-13T0900-talk";

    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly ServiceProvider _provider;
    private long _seq;

    public RunConversationProjectionTests()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        services.AddSingleton(Mock.Of<ITicketActivityNudge>());
        services.AddRunProjections();
        services.AddSingleton<RunEventApplier>();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task RunConversationProjection_SecondPark_KeepsTheFirstQuestion()
    {
        await ApplyAsync(Fetched("42"));
        await ApplyAsync(Parked("q1", "Which client?", "42"));
        await ApplyAsync(Parked("q2", "Which version?", "42"));

        var questions = (await RowsAsync()).Where(r => r.Kind == RunConversationKinds.Question).ToList();
        questions.Select(q => (q.QuestionId, q.Text)).Should().Equal(("q1", "Which client?"), ("q2", "Which version?"));
        (await CheckpointQuestionAsync()).Should().Be("q2", "the checkpoint keeps only the latest question");
    }

    [Fact]
    public async Task RunConversationProjection_ReplayedEvent_WritesOneRow()
    {
        IReadOnlyList<RunEvent> turns =
        [
            Fetched("42"), Reviewed(1, [Finding()]), Reviewed(2, []),
            new SpecHandedBackEvent(RunId, T, "Handed back: the export would destroy the record.", "Delete the log."),
        ];
        for (var round = 0; round < 2; round++)
            for (var i = 0; i < turns.Count; i++) await ApplyAsync(turns[i], seq: i + 1);

        var rows = await RowsAsync();
        rows.Select(r => r.Kind).Should().Equal(RunConversationKinds.TicketText, RunConversationKinds.CutReview,
            RunConversationKinds.CutReview, RunConversationKinds.HandBack);
        rows[1].DetailJson.Should().Contain("\"deliverable\":\"refused\"").And.Contain("\"specId\":\"p42a\"");
        rows[2].DetailJson.Should().Contain("\"deliverable\":\"accepted\"").And.Contain("\"attempt\":2");
        (rows[3].Text, rows[3].DetailJson).Should().Be(
            ("Handed back: the export would destroy the record.", "{\"quote\":\"Delete the log.\"}"));
    }

    [Fact]
    public async Task RunConversationProjection_TicketlessCheckpoint_WritesNothing()
    {
        await ApplyAsync(Parked("q1", "Which client?", ticketId: ""));
        await ApplyAsync(Fetched(""));

        (await RowsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task RunConversationProjection_TicketFetched_StoresDescriptionAndCriteriaNotTitle()
    {
        await ApplyAsync(Fetched("42"));

        var row = (await RowsAsync()).Should().ContainSingle().Subject;
        (row.Kind, row.Text, row.At).Should().Be((RunConversationKinds.TicketText, "Make the export faster.", T));
        row.DetailJson.Should().Be("{\"acceptanceCriteria\":\"It exports in a second.\"}");
        (row.Text + row.DetailJson).Should().NotContain("Export title");
    }

    [Fact]
    public async Task RunDeletion_RemovesConversationEntries()
    {
        await ApplyAsync(Fetched("42"));
        await ApplyAsync(Parked("q1", "Which client?", "42"));

        await using (var ctx = new AgentSmithDbContext(Options()))
            await new RunDeletionRepository(ctx).DeleteAsync(RunId, CancellationToken.None);

        (await RowsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task RunRetention_LeavesConversationEntries()
    {
        await ApplyAsync(Fetched("42"));

        await using (var ctx = new AgentSmithDbContext(Options()))
            await new RunRetentionService(ctx, new FixedClock(T.AddDays(365)))
                .PruneAsync(RunRetentionService.DefaultRetention, CancellationToken.None);

        (await RowsAsync()).Should().ContainSingle("retention prunes the trail, not the conversation");
    }

    private static TicketFetchedEvent Fetched(string ticketId) =>
        new(RunId, ticketId, "Export title", "Make the export faster.", "open", [], 0, "GitHub", T,
            AcceptanceCriteria: "It exports in a second.");

    private static RunCheckpointedEvent Parked(string questionId, string text, string ticketId) =>
        new(RunId, "alpha", ticketId, "github", "code", "job-1", questionId,
            JsonSerializer.Serialize(new DialogQuestion(questionId, QuestionType.FreeText, text, null, null, null,
                TimeSpan.FromHours(1))),
            "[]", "{}", 1, T, T.AddHours(1), T);

    private static SpecCutReviewedEvent Reviewed(int attempt, IReadOnlyList<CutFinding> findings) =>
        new(RunId, T, attempt,
            findings.Count == 0 ? CutReviewDeliverables.Accepted : CutReviewDeliverables.Refused, findings);

    private static CutFinding Finding() => new("p42a", "It exports in a second.", "unmeasurable", "no load named");

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    private async Task ApplyAsync(RunEvent ev, long? seq = null)
    {
        using var scope = _provider.CreateScope();
        await _provider.GetRequiredService<RunEventApplier>().ApplyAsync(
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>(), ev, seq ?? ++_seq, CancellationToken.None);
    }

    private async Task<List<RunConversationEntry>> RowsAsync()
    {
        await using var ctx = new AgentSmithDbContext(Options());
        return await ctx.Set<RunConversationEntry>().Where(r => r.RunId == RunId).OrderBy(r => r.Id).ToListAsync();
    }

    private async Task<string?> CheckpointQuestionAsync()
    {
        await using var ctx = new AgentSmithDbContext(Options());
        return await ctx.Set<RunCheckpoint>().Where(c => c.RunId == RunId).Select(c => c.QuestionId).SingleOrDefaultAsync();
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
