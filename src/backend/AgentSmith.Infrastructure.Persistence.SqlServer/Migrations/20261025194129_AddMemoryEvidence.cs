using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
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
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RepoKey = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Weight = table.Column<double>(type: "float", nullable: false),
                    Project = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    RunId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    SpecId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    EventSeq = table.Column<long>(type: "bigint", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CriterionKey = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: true),
                    At = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
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
