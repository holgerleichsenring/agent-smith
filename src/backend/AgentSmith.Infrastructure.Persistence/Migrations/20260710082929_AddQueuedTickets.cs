using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQueuedTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QueuedTickets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Project = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Pipeline = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Platform = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    ReservedRunId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: true),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    EnqueuedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    InitialContextJson = table.Column<string>(type: "TEXT", nullable: true),
                    PlanAnswersJson = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueuedTickets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QueuedTickets_Project_TicketId",
                table: "QueuedTickets",
                columns: new[] { "Project", "TicketId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QueuedTickets");
        }
    }
}
