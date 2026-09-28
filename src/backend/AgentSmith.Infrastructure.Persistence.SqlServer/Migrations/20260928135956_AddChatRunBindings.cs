using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddChatRunBindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatRunBindings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    Platform = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    ChannelId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    ThreadId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: true),
                    RequestedBy = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    ReplyEndpoint = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    QuestionId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: true),
                    QuestionJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
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
