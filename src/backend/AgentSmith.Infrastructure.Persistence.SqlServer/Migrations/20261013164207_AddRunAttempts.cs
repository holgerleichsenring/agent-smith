using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddRunAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RunAttempts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    CauseChannel = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CauseActor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CauseAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SourceKind = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SourceUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceDetail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TicketFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TrackerState = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WebUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RevisionNumber = table.Column<int>(type: "int", nullable: true),
                    RevisionCause = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SeriesId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: true),
                    CarriedSpecIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovalAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovalConversation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovalPrincipal = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunAttempts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RunAttempts_RunId",
                table: "RunAttempts",
                column: "RunId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RunAttempts");
        }
    }
}
