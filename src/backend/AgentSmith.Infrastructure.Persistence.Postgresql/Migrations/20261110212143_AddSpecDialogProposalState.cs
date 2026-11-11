using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Postgresql.Migrations
{
    /// <inheritdoc />
    public partial class AddSpecDialogProposalState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AwaitingProposalJson",
                table: "SpecDialogSessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PostedAt",
                table: "SpecDialogSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostedProposalId",
                table: "SpecDialogSessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProposalId",
                table: "SpecDialogSessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProposalState",
                table: "SpecDialogSessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewingProposalJson",
                table: "SpecDialogSessions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AwaitingProposalJson",
                table: "SpecDialogSessions");

            migrationBuilder.DropColumn(
                name: "PostedAt",
                table: "SpecDialogSessions");

            migrationBuilder.DropColumn(
                name: "PostedProposalId",
                table: "SpecDialogSessions");

            migrationBuilder.DropColumn(
                name: "ProposalId",
                table: "SpecDialogSessions");

            migrationBuilder.DropColumn(
                name: "ProposalState",
                table: "SpecDialogSessions");

            migrationBuilder.DropColumn(
                name: "ReviewingProposalJson",
                table: "SpecDialogSessions");
        }
    }
}
