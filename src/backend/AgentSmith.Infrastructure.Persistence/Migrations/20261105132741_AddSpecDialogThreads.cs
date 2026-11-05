using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSpecDialogThreads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SpecDialogThread",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessionId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Stewarded = table.Column<bool>(type: "INTEGER", nullable: false),
                    MessagesJson = table.Column<string>(type: "TEXT", nullable: false),
                    TranscriptFrom = table.Column<int>(type: "INTEGER", nullable: false),
                    ReplyAt = table.Column<int>(type: "INTEGER", nullable: true),
                    WrittenAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecDialogThread", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SpecDialogThread_SessionId",
                table: "SpecDialogThread",
                column: "SessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SpecDialogThread");
        }
    }
}
