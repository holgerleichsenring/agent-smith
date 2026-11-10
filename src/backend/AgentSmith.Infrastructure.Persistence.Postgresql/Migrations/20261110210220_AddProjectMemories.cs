using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Postgresql.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectMemories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectMemories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<long>(type: "bigint", nullable: false),
                    Slug = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    SourceKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    SourceRef = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: true),
                    Author = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectMemories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectMemories_ConfigEntities_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "ConfigEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMemories_ProjectId_Slug",
                table: "ProjectMemories",
                columns: new[] { "ProjectId", "Slug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectMemories");
        }
    }
}
