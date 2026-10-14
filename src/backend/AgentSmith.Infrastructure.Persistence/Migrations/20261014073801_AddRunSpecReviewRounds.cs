using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRunSpecReviewRounds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RunSpecReviewRounds",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RunId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    EventSeq = table.Column<long>(type: "INTEGER", nullable: true),
                    SpecId = table.Column<string>(type: "TEXT", nullable: true),
                    Scope = table.Column<string>(type: "TEXT", nullable: false),
                    Round = table.Column<int>(type: "INTEGER", nullable: false),
                    Reviewer = table.Column<int>(type: "INTEGER", nullable: false),
                    Verdict = table.Column<string>(type: "TEXT", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CutsJson = table.Column<string>(type: "TEXT", nullable: false),
                    FindingsJson = table.Column<string>(type: "TEXT", nullable: false),
                    EvidenceProblemsJson = table.Column<string>(type: "TEXT", nullable: false),
                    ResiduesJson = table.Column<string>(type: "TEXT", nullable: false),
                    RevisionNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunSpecReviewRounds", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RunSpecReviewRounds_RunId",
                table: "RunSpecReviewRounds",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_RunSpecReviewRounds_RunId_EventSeq",
                table: "RunSpecReviewRounds",
                columns: new[] { "RunId", "EventSeq" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RunSpecReviewRounds");
        }
    }
}
