using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
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
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SpecKey = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    Tracker = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    RecordJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ApprovedInConversation = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
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
