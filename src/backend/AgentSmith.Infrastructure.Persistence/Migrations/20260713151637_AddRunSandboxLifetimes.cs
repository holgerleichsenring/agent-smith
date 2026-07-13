using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRunSandboxLifetimes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DisposedAt",
                table: "RunSandboxes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MemoryRequest",
                table: "RunSandboxes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SpawnedAt",
                table: "RunSandboxes",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisposedAt",
                table: "RunSandboxes");

            migrationBuilder.DropColumn(
                name: "MemoryRequest",
                table: "RunSandboxes");

            migrationBuilder.DropColumn(
                name: "SpawnedAt",
                table: "RunSandboxes");
        }
    }
}
