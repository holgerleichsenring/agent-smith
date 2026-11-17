using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-11-17-a0b6d: MoveProposalsToOneTable on the SQLite set — a store at the head before it, holding a proposal in
/// every shape the session row kept one, migrates into rows with their ids and states and reads back; going down puts
/// them back on the session row.
/// </summary>
public sealed class ProposalMigrationTests : IDisposable
{
    private const string Before = "AddRunEndDetailJson";
    private const string Move = "MoveProposalsToOneTable";

    private static readonly string AwaitingJson = Json("awaiting");
    private static readonly string FilingJson = Json("filing");
    private static readonly string ReviewingJson = OutcomeProposalJson.Write(Spec("reviewing") with { UnderReview = true });
    private static readonly string LatestJson = Json("latest");
    private static readonly string LatestDraftJson = OutcomeProposalJson.Write(Spec("latest draft") with { UnderReview = true });

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AgentSmithDbContext _context;

    public ProposalMigrationTests()
    {
        _connection.Open();
        _context = new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        Migrator().Migrate(Before);
        SeedOldShape();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Up_EveryProposalShape_BecomesARowWithItsIdAndState()
    {
        Migrator().Migrate(Move);

        var rows = await _context.Set<SpecDialogProposal>().AsNoTracking().ToListAsync();
        rows.Select(r => (r.SessionId, r.RunId, r.ProposalId, r.State, r.ProposalJson)).Should().BeEquivalentTo(
        [
            ("s-await", "run-a", "a1", SpecDialogProposalClaim.Awaiting, AwaitingJson),
            ("s-file", "proposal:f1", "f1", SpecDialogProposalClaim.Filing, FilingJson),
            ("s-review", "proposal:review:s-review", (string?)null, SpecDialogProposalClaim.Reviewing, ReviewingJson),
            ("s-latest", "proposal:migrated-s-latest", "migrated-s-latest", SpecDialogProposalClaim.Awaiting, LatestJson),
            ("s-draft", "proposal:review:s-draft", (string?)null, SpecDialogProposalClaim.Reviewing, LatestDraftJson),
        ], "the awaiting proposal lands on the turn row whose JSON is its own; every other one on a row of its own");
        rows.Select(r => OutcomeProposalJson.Read(r.ProposalJson)).Should().HaveCount(5, "every stored proposal still reads");
        (await new SpecDialogProposalClaim(_context).ReadAsync("web", "t-latest", default)).AwaitingId
            .Should().Be("migrated-s-latest", "a proposal from before ccd9 is decidable under its minted id");
    }

    [Fact]
    public async Task Down_PutsTheProposalsBackOnTheSessionRow()
    {
        Migrator().Migrate(Move);

        Migrator().Migrate(Before);

        (await Scalar("SELECT ProposalId || '|' || ProposalState || '|' || (AwaitingProposalJson = LatestProposalJson) FROM SpecDialogSessions WHERE SessionId = 's-await'"))
            .Should().Be("a1|awaiting|1");
        (await Scalar("SELECT ConfirmedProposalId || '|' || ProposalState FROM SpecDialogSessions WHERE SessionId = 's-file'"))
            .Should().Be("f1|filing");
        (await Scalar("SELECT ReviewingProposalJson FROM SpecDialogSessions WHERE SessionId = 's-review'")).Should().Be(ReviewingJson);
        (await Scalar("SELECT group_concat(RunId) FROM SpecDialogTurnProposal")).Should().Be("run-a", "only the rows a run wrote stay");
    }

    private IMigrator Migrator() => _context.GetService<IMigrator>();

    private async Task<string?> Scalar(string sql)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = sql;
        return (await command.ExecuteScalarAsync())?.ToString();
    }

    private void SeedOldShape()
    {
        const string columns = "SessionId, Platform, ChannelId, ThreadId, UserId, Project, ReposJson, TranscriptJson, IsOpen, LastActivityAt, CreatedAt, UpdatedAt";
        const string at = "'2026-11-16 09:00:00+00:00'";
        string Session(string id, string extraColumns, string extraValues) =>
            $"INSERT INTO SpecDialogSessions ({columns}{extraColumns}) VALUES ('{id}', 'web', 'c', 't-{id[2..]}', 'u', 'p', '[]', '[]', 1, {at}, {at}, {at}{extraValues});";
        using var command = _connection.CreateCommand();
        command.CommandText = string.Join('\n',
            Session("s-await", ", ProposalId, ProposalState, AwaitingProposalJson, LatestProposalJson", $", 'a1', 'awaiting', {Q(AwaitingJson)}, {Q(AwaitingJson)}"),
            Session("s-file", ", ProposalId, ProposalState, ConfirmedOutcomeJson, ConfirmedProposalId", $", 'f1', 'filing', {Q(FilingJson)}, 'f1'"),
            Session("s-review", ", ReviewingProposalJson", $", {Q(ReviewingJson)}"),
            Session("s-latest", ", LatestProposalJson", $", {Q(LatestJson)}"),
            Session("s-draft", ", LatestProposalJson", $", {Q(LatestDraftJson)}"),
            $"INSERT INTO SpecDialogTurnProposal (SessionId, RunId, ProposalJson, CreatedAt, UpdatedAt) VALUES ('s-await', 'run-a', {Q(AwaitingJson)}, {at}, {at});");
        command.ExecuteNonQuery();
    }

    private static string Q(string text) => $"'{text.Replace("'", "''")}'";

    private static SpecOutcome Spec(string goal) => new(new SpecDraft("2026-11-17-a0b6x", goal, "spec: x", []));

    private static string Json(string goal) => OutcomeProposalJson.Write(Spec(goal));
}
