using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
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
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    EventSeq = table.Column<long>(type: "bigint", nullable: true),
                    SpecId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Scope = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Round = table.Column<int>(type: "int", nullable: false),
                    Reviewer = table.Column<int>(type: "int", nullable: false),
                    Verdict = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CutsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FindingsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EvidenceProblemsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResiduesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
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
                unique: true,
                filter: "[EventSeq] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RunSpecReviewRounds");
        }
    }
}
