using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTakenTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 2026-09-25-b4d9: NO type argument on the length-declared string columns,
            // deliberately — the same reason AddSpecDialogSubject gives. This set is generated
            // from SQLite and three providers run it verbatim, so a literal here is
            // provider-blind: "TEXT" on MySQL is a sixty-four-kilobyte blob type that ignores
            // the length beside it, and the length is the whole declaration of these columns.
            // Untyped, each provider's own convention maps a bounded string to its own type.
            migrationBuilder.CreateTable(
                name: "TakenTickets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Project = table.Column<string>(maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(maxLength: 191, nullable: false),
                    Platform = table.Column<string>(maxLength: 191, nullable: false),
                    Pipeline = table.Column<string>(maxLength: 191, nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TakenTickets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TakenTickets_Project_TicketId",
                table: "TakenTickets",
                columns: new[] { "Project", "TicketId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TakenTickets");
        }
    }
}
