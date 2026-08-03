using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class RenameTicketWorkSpecToSpecSet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // p0393a drops the second spec vocabulary: one word for one thing. The rows are
            // pointers at specs that live in git, so a RENAME (not a drop and re-create)
            // keeps the "is the branch artifact my own last revision or a reviewer's edit"
            // answer intact across the upgrade.
            migrationBuilder.RenameTable(
                name: "TicketWorkSpecs",
                newName: "TicketSpecSets");

            migrationBuilder.RenameIndex(
                name: "IX_TicketWorkSpecs_Project_SpecKey",
                table: "TicketSpecSets",
                newName: "IX_TicketSpecSets_Project_SpecKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_TicketSpecSets_Project_SpecKey",
                table: "TicketSpecSets",
                newName: "IX_TicketWorkSpecs_Project_SpecKey");

            migrationBuilder.RenameTable(
                name: "TicketSpecSets",
                newName: "TicketWorkSpecs");
        }
    }
}
