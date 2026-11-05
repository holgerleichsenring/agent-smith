using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
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
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    Stewarded = table.Column<bool>(type: "bit", nullable: false),
                    MessagesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TranscriptFrom = table.Column<int>(type: "int", nullable: false),
                    ReplyAt = table.Column<int>(type: "int", nullable: true),
                    WrittenAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
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
