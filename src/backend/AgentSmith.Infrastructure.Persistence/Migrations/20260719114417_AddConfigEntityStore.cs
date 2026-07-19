using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConfigEntityStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfigEntities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    EntityId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Doc = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedBy = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigEntities", x => x.Id);
                    table.UniqueConstraint("AK_ConfigEntities_EntityType_EntityId", x => new { x.EntityType, x.EntityId });
                });

            migrationBuilder.CreateTable(
                name: "ConfigEntityVersions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    EntityId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    Doc = table.Column<string>(type: "TEXT", nullable: true),
                    ChangedBy = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Note = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigEntityVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConfigRefs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FromType = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    FromId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    ToType = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    ToId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigRefs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConfigRefs_ConfigEntities_ToType_ToId",
                        columns: x => new { x.ToType, x.ToId },
                        principalTable: "ConfigEntities",
                        principalColumns: new[] { "EntityType", "EntityId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConfigEntities_EntityType_EntityId",
                table: "ConfigEntities",
                columns: new[] { "EntityType", "EntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConfigEntityVersions_EntityType_EntityId_Version",
                table: "ConfigEntityVersions",
                columns: new[] { "EntityType", "EntityId", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_ConfigRefs_FromType_FromId",
                table: "ConfigRefs",
                columns: new[] { "FromType", "FromId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConfigRefs_ToType_ToId",
                table: "ConfigRefs",
                columns: new[] { "ToType", "ToId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfigEntityVersions");

            migrationBuilder.DropTable(
                name: "ConfigRefs");

            migrationBuilder.DropTable(
                name: "ConfigEntities");
        }
    }
}
