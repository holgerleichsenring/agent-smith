using AgentSmith.Infrastructure.Persistence.Migrations.Support;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// 2026-11-17-a0b6d: a conversation's proposals live in one table. SpecDialogTurnProposal becomes SpecDialogProposal
    /// with a proposal id and a state; the session's seven proposal columns are copied into rows
    /// (<see cref="ProposalTableMoveSql"/>) and dropped, and the one-awaiting index is created after the copy. Going down
    /// puts them back on the session and drops the rows no run wrote.
    /// </summary>
    public partial class MoveProposalsToOneTable : Migration
    {
        private const string MySqlProvider = "Pomelo.EntityFrameworkCore.MySql";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SpecDialogTurnProposal_RunId",
                table: "SpecDialogTurnProposal");

            migrationBuilder.DropIndex(
                name: "IX_SpecDialogTurnProposal_SessionId",
                table: "SpecDialogTurnProposal");

            migrationBuilder.RenameTable(
                name: "SpecDialogTurnProposal",
                newName: "SpecDialogProposal");

            migrationBuilder.AddColumn<string>(
                name: "ProposalId",
                table: "SpecDialogProposal",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "SpecDialogProposal",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            foreach (var sql in ProposalTableMoveSql.ForSharedSet(migrationBuilder.ActiveProvider).Up())
                migrationBuilder.Sql(sql);

            migrationBuilder.DropColumn(
                name: "AwaitingProposalJson",
                table: "SpecDialogSessions");

            migrationBuilder.DropColumn(
                name: "ConfirmedOutcomeJson",
                table: "SpecDialogSessions");

            migrationBuilder.DropColumn(
                name: "ConfirmedProposalId",
                table: "SpecDialogSessions");

            migrationBuilder.DropColumn(
                name: "LatestProposalJson",
                table: "SpecDialogSessions");

            migrationBuilder.DropColumn(
                name: "ProposalId",
                table: "SpecDialogSessions");

            migrationBuilder.DropColumn(
                name: "ProposalState",
                table: "SpecDialogSessions");

            migrationBuilder.DropColumn(
                name: "ReviewingProposalJson",
                table: "SpecDialogSessions");

            migrationBuilder.CreateIndex(
                name: "IX_SpecDialogProposal_ProposalId",
                table: "SpecDialogProposal",
                column: "ProposalId");

            migrationBuilder.CreateIndex(
                name: "IX_SpecDialogProposal_RunId",
                table: "SpecDialogProposal",
                column: "RunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpecDialogProposal_SessionId",
                table: "SpecDialogProposal",
                column: "SessionId");

            // MySQL runs this set too and has no partial index; it keeps one awaiting row by compare-and-set alone.
            if (migrationBuilder.ActiveProvider != MySqlProvider)
                migrationBuilder.CreateIndex(
                    name: "IX_SpecDialogProposal_OneAwaiting",
                    table: "SpecDialogProposal",
                    column: "SessionId",
                    unique: true,
                    filter: "\"State\" = 'awaiting'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider != MySqlProvider)
                migrationBuilder.DropIndex(
                    name: "IX_SpecDialogProposal_OneAwaiting",
                    table: "SpecDialogProposal");

            migrationBuilder.DropIndex(
                name: "IX_SpecDialogProposal_ProposalId",
                table: "SpecDialogProposal");

            migrationBuilder.DropIndex(
                name: "IX_SpecDialogProposal_RunId",
                table: "SpecDialogProposal");

            migrationBuilder.DropIndex(
                name: "IX_SpecDialogProposal_SessionId",
                table: "SpecDialogProposal");

            migrationBuilder.AddColumn<string>(
                name: "AwaitingProposalJson",
                table: "SpecDialogSessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfirmedOutcomeJson",
                table: "SpecDialogSessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfirmedProposalId",
                table: "SpecDialogSessions",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LatestProposalJson",
                table: "SpecDialogSessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProposalId",
                table: "SpecDialogSessions",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProposalState",
                table: "SpecDialogSessions",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewingProposalJson",
                table: "SpecDialogSessions",
                type: "TEXT",
                nullable: true);

            foreach (var sql in ProposalTableMoveSql.ForSharedSet(migrationBuilder.ActiveProvider).Down())
                migrationBuilder.Sql(sql);

            migrationBuilder.DropColumn(
                name: "ProposalId",
                table: "SpecDialogProposal");

            migrationBuilder.DropColumn(
                name: "State",
                table: "SpecDialogProposal");

            migrationBuilder.RenameTable(
                name: "SpecDialogProposal",
                newName: "SpecDialogTurnProposal");

            migrationBuilder.CreateIndex(
                name: "IX_SpecDialogTurnProposal_RunId",
                table: "SpecDialogTurnProposal",
                column: "RunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpecDialogTurnProposal_SessionId",
                table: "SpecDialogTurnProposal",
                column: "SessionId");
        }
    }
}
