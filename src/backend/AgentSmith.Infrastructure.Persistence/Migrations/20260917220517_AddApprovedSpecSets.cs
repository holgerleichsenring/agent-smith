using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovedSpecSets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApprovedSpecSets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SpecKey = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Tracker = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    RecordJson = table.Column<string>(type: "TEXT", nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ApprovedInConversation = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    ApprovedBy = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovedSpecSets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedSpecSets_Tracker_SpecKey",
                table: "ApprovedSpecSets",
                columns: new[] { "Tracker", "SpecKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovedSpecSets");
        }
    }
}
