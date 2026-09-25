using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovedSpecSetTicketId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SatisfiedAt",
                table: "ApprovedSpecSets",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TicketId",
                table: "ApprovedSpecSets",
                type: "nvarchar(191)",
                maxLength: 191,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedSpecSets_Tracker_SatisfiedAt",
                table: "ApprovedSpecSets",
                columns: new[] { "Tracker", "SatisfiedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApprovedSpecSets_Tracker_SatisfiedAt",
                table: "ApprovedSpecSets");

            migrationBuilder.DropColumn(
                name: "SatisfiedAt",
                table: "ApprovedSpecSets");

            migrationBuilder.DropColumn(
                name: "TicketId",
                table: "ApprovedSpecSets");
        }
    }
}
