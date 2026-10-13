using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddRunConversationEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RunConversationEntry",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    EventSeq = table.Column<long>(type: "bigint", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    QuestionId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DetailJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunConversationEntry", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RunConversationEntry_RunId",
                table: "RunConversationEntry",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_RunConversationEntry_RunId_EventSeq",
                table: "RunConversationEntry",
                columns: new[] { "RunId", "EventSeq" },
                unique: true,
                filter: "[EventSeq] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RunConversationEntry");
        }
    }
}
