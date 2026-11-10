using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPromptRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PromptBlocks",
                columns: table => new
                {
                    Hash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Chars = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptBlocks", x => x.Hash);
                });

            migrationBuilder.CreateTable(
                name: "PromptRecordBlocks",
                columns: table => new
                {
                    RecordId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    BlockHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptRecordBlocks", x => new { x.RecordId, x.Position });
                });

            migrationBuilder.CreateTable(
                name: "PromptRecords",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    RunId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    ParentId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ParentLength = table.Column<int>(type: "INTEGER", nullable: false),
                    ToolCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PromptRecordBlocks_BlockHash",
                table: "PromptRecordBlocks",
                column: "BlockHash");

            migrationBuilder.CreateIndex(
                name: "IX_PromptRecords_RunId",
                table: "PromptRecords",
                column: "RunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PromptBlocks");

            migrationBuilder.DropTable(
                name: "PromptRecordBlocks");

            migrationBuilder.DropTable(
                name: "PromptRecords");
        }
    }
}
