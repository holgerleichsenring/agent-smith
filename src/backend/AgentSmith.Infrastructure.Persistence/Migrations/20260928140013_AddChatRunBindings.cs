using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChatRunBindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No type argument on the length-declared string columns, as in AddTakenTickets: this
            // set is generated from SQLite and three providers run it verbatim, and "TEXT" on
            // MySQL is a blob type that ignores the length beside it. Untyped, each provider maps
            // a bounded string to its own type.
            migrationBuilder.CreateTable(
                name: "ChatRunBindings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RunId = table.Column<string>(maxLength: 191, nullable: false),
                    Platform = table.Column<string>(maxLength: 191, nullable: false),
                    ChannelId = table.Column<string>(maxLength: 191, nullable: false),
                    ThreadId = table.Column<string>(maxLength: 191, nullable: true),
                    RequestedBy = table.Column<string>(maxLength: 191, nullable: false),
                    ReplyEndpoint = table.Column<string>(maxLength: 512, nullable: true),
                    QuestionId = table.Column<string>(maxLength: 191, nullable: true),
                    QuestionJson = table.Column<string>(type: "TEXT", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatRunBindings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatRunBindings_ClosedAt",
                table: "ChatRunBindings",
                column: "ClosedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRunBindings_Platform_ChannelId_ThreadId",
                table: "ChatRunBindings",
                columns: new[] { "Platform", "ChannelId", "ThreadId" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatRunBindings_RunId",
                table: "ChatRunBindings",
                column: "RunId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatRunBindings");
        }
    }
}
