using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovedSpecSetTicketId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 2026-09-25-c1f7: SatisfiedAt keeps the generated type literal, like every other
            // DateTimeOffset column of this table (CreatedAt, UpdatedAt, ApprovedAt) — one table
            // spelling one type two ways is worse than the spelling itself.
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SatisfiedAt",
                table: "ApprovedSpecSets",
                type: "TEXT",
                nullable: true);

            // 2026-09-25-c1f7: NO type argument, deliberately — the same reason
            // AddSpecDialogSubject gives. This set is generated from SQLite and three providers run
            // it verbatim, so a literal here is provider-blind: "TEXT" on MySQL is a
            // sixty-four-kilobyte blob type that ignores the length beside it, and the length is
            // the whole declaration of this column. Untyped, each provider's own convention maps a
            // bounded string to that provider's bounded-string type.
            migrationBuilder.AddColumn<string>(
                name: "TicketId",
                table: "ApprovedSpecSets",
                maxLength: 191,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedSpecSets_Tracker_SatisfiedAt",
                table: "ApprovedSpecSets",
                columns: new[] { "Tracker", "SatisfiedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApprovedSpecSets_Tracker_SatisfiedAt",
                table: "ApprovedSpecSets");

            migrationBuilder.DropColumn(
                name: "SatisfiedAt",
                table: "ApprovedSpecSets");

            migrationBuilder.DropColumn(
                name: "TicketId",
                table: "ApprovedSpecSets");
        }
    }
}
