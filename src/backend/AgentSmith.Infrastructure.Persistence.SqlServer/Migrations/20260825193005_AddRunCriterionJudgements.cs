using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddRunCriterionJudgements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RunCriterionJudgements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    CriterionKey = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    CriterionText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MachineStatus = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    HumanStatus = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Author = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunCriterionJudgements", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RunCriterionJudgements_RunId_CriterionKey",
                table: "RunCriterionJudgements",
                columns: new[] { "RunId", "CriterionKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RunCriterionJudgements");
        }
    }
}
