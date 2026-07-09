using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSpecDialogSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SpecDialogSessions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessionId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Platform = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    ChannelId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    ThreadId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Project = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    ReposJson = table.Column<string>(type: "TEXT", nullable: false),
                    TranscriptJson = table.Column<string>(type: "TEXT", nullable: false),
                    IsOpen = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastActivityAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecDialogSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SpecDialogSessions_Platform_ThreadId",
                table: "SpecDialogSessions",
                columns: new[] { "Platform", "ThreadId" });

            migrationBuilder.CreateIndex(
                name: "IX_SpecDialogSessions_SessionId",
                table: "SpecDialogSessions",
                column: "SessionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SpecDialogSessions");
        }
    }
}
