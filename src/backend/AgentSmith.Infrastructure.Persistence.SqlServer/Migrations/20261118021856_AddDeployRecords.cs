using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddDeployRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeployRecords",
                columns: table => new
                {
                    RunId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    Project = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    Context = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    ContextRepo = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: true),
                    Environment = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    Delivery = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Repo = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PullRequestUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PipelineRunId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PullRequestState = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    CiState = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CiFailuresJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeliveredAtTicks = table.Column<long>(type: "bigint", nullable: false),
                    CheckedAtTicks = table.Column<long>(type: "bigint", nullable: true),
                    SettledAtTicks = table.Column<long>(type: "bigint", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeployRecords", x => x.RunId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeployRecords_SettledAtTicks",
                table: "DeployRecords",
                column: "SettledAtTicks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeployRecords");
        }
    }
}
