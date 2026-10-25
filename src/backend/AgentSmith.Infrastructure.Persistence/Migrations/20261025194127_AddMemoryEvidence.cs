using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMemoryEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MemoryEvidence",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RepoKey = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Weight = table.Column<double>(type: "REAL", nullable: false),
                    Project = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    RunId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    SpecId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    EventSeq = table.Column<long>(type: "INTEGER", nullable: true),
                    Reason = table.Column<string>(type: "TEXT", nullable: true),
                    CriterionKey = table.Column<string>(type: "TEXT", maxLength: 191, nullable: true),
                    At = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoryEvidence", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEvidence_RepoKey_Slug",
                table: "MemoryEvidence",
                columns: new[] { "RepoKey", "Slug" });

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEvidence_RunId_SpecId",
                table: "MemoryEvidence",
                columns: new[] { "RunId", "SpecId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemoryEvidence");
        }
    }
}
