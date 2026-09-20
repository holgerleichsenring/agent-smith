using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSpecDialogAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SpecDialogAttachments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessionId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    MediaType = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    // 2026-09-20-3af8: NO type argument, deliberately. This set is generated
                    // from SQLite and three providers run it verbatim, so a literal here is
                    // provider-blind — and "TEXT" means sixty-four kilobytes on MySQL, about a
                    // hundredth of an encoded five-megabyte image: an error in strict mode and a
                    // SILENT truncation otherwise. Untyped, each provider's own convention maps
                    // an unbounded string to that provider's large-text type.
                    ContentBase64 = table.Column<string>(nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecDialogAttachments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SpecDialogAttachments_SessionId",
                table: "SpecDialogAttachments",
                column: "SessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SpecDialogAttachments");
        }
    }
}
